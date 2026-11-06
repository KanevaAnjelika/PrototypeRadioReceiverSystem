#include <Wire.h>
#include <LiquidCrystal_I2C.h>
#include <IRremote.hpp>
#include <EEPROM.h>

LiquidCrystal_I2C lcd(0x27, 16, 2);
#define TEA5767_ADDR 0x60
#define IR_PIN 2

// ============================================================
// Протокол WPF ↔ Arduino (Serial, 9600 бод, строки с '\n')
//
// WPF → Arduino (команды):
//   UP         — частота +0.1 МГц
//   DOWN       — частота -0.1 МГц
//   SCAN       — запустить/остановить автопоиск
//   SAVE       — сохранить текущую частоту в EEPROM
//   LOAD       — загрузить последнюю частоту из EEPROM
//   SET <freq> — установить точную частоту, например «SET 101.5»
//   STATUS     — опросить статус немедленно
//
// Arduino → WPF (ответы):
//   <freq>,<rssi 0..10>,<stereo 0|1>   — строка статуса
//   EVT,<code>[,<arg>]                  — событие: UP, DOWN, SET, SAVED,
//                                         LOADED, SCAN_START, SCAN_END,
//                                         SCAN_NONE, WDT_RESET, BUS_RESET
//   ERR,<text>                          — ошибка: UNKNOWN_CMD, INVALID_FREQ
// ============================================================

#define SERIAL_BAUD 9600
#define STATUS_INTERVAL_MS 400
#define SCAN_STEP_MS 150
#define RSSI_MAX_LEVEL 10
#define SCAN_RSSI_THRESHOLD 5   // сырой RSSI (0..15), ≈ 3/10 на шкале WPF

// EEPROM
#define EEPROM_MAGIC_ADDR 0
#define EEPROM_FREQ_ADDR 4
#define EEPROM_MAGIC_VALUE 0xA5

class DataStorage {
  public:
    void saveLastStation(float freq) {
      if (isnan(freq)) return;
      int tenths = (int)(freq * 10.0 + 0.5);
      EEPROM.put(EEPROM_FREQ_ADDR, (uint16_t)tenths);
      EEPROM.write(EEPROM_MAGIC_ADDR, EEPROM_MAGIC_VALUE);
    }
    float loadLastStation() {
      if (EEPROM.read(EEPROM_MAGIC_ADDR) != EEPROM_MAGIC_VALUE) return 102.0;
      uint16_t tenths = 0;
      EEPROM.get(EEPROM_FREQ_ADDR, tenths);
      if (tenths < 760 || tenths > 1080) return 102.0;
      return tenths / 10.0;
    }
};

class ModuleControl {
    private:
    int _signalLevel = 0;
    bool _isStereo = false;
    float _currentFreq = 102.0;
    int _currentIndex = 0;
    const float _stations[3] = {102.0, 102.5, 103.5};
    const int _stationCount = 3;
    
    // Переменные для "сторожевого таймера"
    unsigned long _lastValidSignalTime = 0;
    bool _isWaitingForSignal = false;

    // Счётчик последовательных неудач чтения — если чип не отвечает,
    // значит I2C-шина зависла или чип выключен
    int _readFailCount = 0;
    const int _READ_FAIL_THRESHOLD = 8;

    // ---------- Автопоиск ----------
    bool _scanActive = false;
    bool _scanWrapped = false;
    float _scanStartFreq = 102.0;
    unsigned long _scanLastStep = 0;

    // Отправка базовых регистров
    void writeRegisters(byte byte3, byte byte4) {
      unsigned int frequencyB = 4 * (_currentFreq * 1000000 + 225000) / 32768;
      byte frequencyH = frequencyB >> 8;
      byte frequencyL = frequencyB & 0xFF;

      Wire.beginTransmission(TEA5767_ADDR);
      Wire.write(frequencyH);
      Wire.write(frequencyL);
      Wire.write(byte3);
      Wire.write(byte4);
      Wire.write(0x00);
      Wire.endTransmission();
    }

    // Полный сброс чипа TEA5767
    // TEA5767 может "зависнуть" после переподключения USB — нужно
    // многократно отправить пакет регистров, чтобы вернуть его в рабочее состояние.
    void resetChip() {
      // 1. "Разбудка": шлём пустой tuning-пакет несколько раз,
      //    чтобы сбросить внутренний сдвиговый регистр чипа
      for (int attempt = 0; attempt < 5; attempt++) {
        Wire.beginTransmission(TEA5767_ADDR);
        Wire.write(0x00);
        Wire.write(0x00);
        Wire.write(0xB0); // byte2: crystal 32.768 kHz
        Wire.write(0x00); // byte3: soft mute ON (глушит шум до настройки)
        Wire.write(0x00);
        Wire.endTransmission();
        delay(50);
      }
      
      // 2. Настройка на рабочую частоту с выключенным soft mute (0x10)
      writeRegisters(0xB0, 0x10);
      delay(300);
      writeRegisters(0xB0, 0x10);
      delay(300);
    }

    // Проверка: отвечает ли чип TEA5767 на I2C-запрос
    bool isChipAlive() {
      Wire.beginTransmission(TEA5767_ADDR);
      byte result = Wire.endTransmission();
      return (result == 0);
    }

    // Линии I2C "прижатые" = шина заблокирована зависшим устройством
    // На Arduino Uno: A4 = SDA, A5 = SCL
    bool isBusStuck() {
      pinMode(A4, INPUT_PULLUP);
      pinMode(A5, INPUT_PULLUP);
      delay(2);
      bool stuck = (digitalRead(A4) == LOW) || (digitalRead(A5) == LOW);
      return stuck;
    }

    // Аварийное освобождение шины: 14 тактов SCL заставляют зависшее
    // ведомое устройство завершить оборванный транзакцию и отпустить SDA
    void recoverI2CBus() {
      pinMode(A4, OUTPUT); // SDA
      pinMode(A5, OUTPUT); // SCL
      digitalWrite(A4, HIGH); // освобождаем SDA
      for (int i = 0; i < 14; i++) {
        digitalWrite(A5, LOW);
        delay(2);
        digitalWrite(A5, HIGH);
        delay(2);
      }
      // STOP-условие: SDA rises while SCL is high
      digitalWrite(A4, LOW);
      delay(2);
      digitalWrite(A5, HIGH);
      delay(2);
      digitalWrite(A4, HIGH);
      delay(2);
      // Возвращаем пины в режим с подтяжкой (как Wire.begin)
      pinMode(A4, INPUT_PULLUP);
      pinMode(A5, INPUT_PULLUP);
      delay(100);
    }

  public:
    void begin() {
      Wire.begin();
      delay(1000); // ВАЖНО: Ждем стабилизации питания USB

      // Если линии прижаты — транзакции Wire зависнут навсегда, чиним шину первыми
      if (isBusStuck()) {
        Serial.println(F("EVT,BUS_STUCK"));
        recoverI2CBus();
      }

      if (isChipAlive()) {
        resetChip();
      } else {
        // Чип молчит: ещё раз освобощаем шину и пробуем инициализировать вслепую
        recoverI2CBus();
        resetChip();
      }
    }

    void setFrequency(float freq) {
      if (isnan(freq)) return;
      if (freq < 76.0) freq = 76.0;
      if (freq > 108.0) freq = 108.0;
      _currentFreq = freq;
      
      writeRegisters(0xB0, 0x10);
      delay(120);
      
      // Запускаем таймер ожидания сигнала
      _lastValidSignalTime = millis();
      _isWaitingForSignal = true;
    }

    void stepUp()   { setFrequency(_currentFreq + 0.1); }
    void stepDown() { setFrequency(_currentFreq - 0.1); }

    void nextStation() {
      _currentIndex = (_currentIndex + 1) % _stationCount;
      setFrequency(_stations[_currentIndex]);
    }

    void startScan() {
      _scanActive = true;
      _scanWrapped = false;
      _scanStartFreq = _currentFreq;
      _scanLastStep = millis();
    }

    void stopScan() { _scanActive = false; }
    bool isScanning() const { return _scanActive; }

    // Шаг автопоиска. Возвращает:
    //   1 — станция найдена, 0 — идём дальше, -1 — станций нет во всём диапазоне
    int scanStep() {
      if (!_scanActive) return 0;

      // Не чаще, чем раз в SCAN_STEP_MS (иначе чип не успевает перестроиться)
      if (millis() - _scanLastStep < SCAN_STEP_MS) return 0;
      _scanLastStep = millis();

      // Ищем шаг вверх, при переходе через 108.0 заворачиваем на 76.0
      float next = _currentFreq + 0.1;
      if (next > 108.0) {
        next = 76.0;
        _scanWrapped = true;
      }
      setFrequency(next);
      readStatus();

      // Полный круг без находки — стоп
      if (_scanWrapped && _currentFreq >= _scanStartFreq) {
        _scanActive = false;
        return -1;
      }

      // Порог по СЫРОМУ RSSI (0..15 от чипа): 5 ≈ «3 из 10» на шкале WPF.
      // Реальная FM-станция даёт 8..15, шум — 0..4, так что 5 отсекает тишину.
      if (_signalLevel >= SCAN_RSSI_THRESHOLD) {
        _scanActive = false;
        return 1;
      }

      return 0;
    }

    void readStatus() {
      Wire.requestFrom(TEA5767_ADDR, 5);
      if (Wire.available() >= 5) {
        Wire.read(); Wire.read();
        byte byte3 = Wire.read();
        byte byte4 = Wire.read();
        Wire.read();

        _signalLevel = byte4 >> 4;
        _isStereo    = byte3 & 0x80;
        
        // Сбрасываем счётчик неудач — чип отвечает
        _readFailCount = 0;
        
        // Если сигнал появился - сбрасываем сторожевой таймер
        if (_signalLevel > 0) {
          _isWaitingForSignal = false;
        }
      } else {
        // Чип не ответил — увеличиваем счётчик
        _readFailCount++;
      }
    }

    // Вызывать в основном цикле loop()
    void checkSignalWatchdog() {
      // Сценарий 1: чип молчит (шина зависла / чип не отвечает)
      if (_readFailCount >= _READ_FAIL_THRESHOLD) {
        Serial.println(F("EVT,BUS_RESET"));
        if (isBusStuck()) recoverI2CBus();
        Wire.begin();
        resetChip();
        _readFailCount = 0;
        _lastValidSignalTime = millis();
        _isWaitingForSignal = true;
        return;
      }

      // Сценарий 2: чип отвечает, но сигнала нет — он в mute/зависании
      if (_isWaitingForSignal && (millis() - _lastValidSignalTime > 3000)) {
        Serial.println(F("EVT,WDT_RESET"));
        resetChip();
        _isWaitingForSignal = false;
      }
    }

    float getFrequency()   { return _currentFreq; }
    int   getSignalLevel() { return _signalLevel; }
    bool  getStereo()      { return _isStereo; }
};

// ============================================================
// SerialBridge — приём команд из WPF и отправка статуса
// Объявляется ПОСЛЕ ModuleControl/DataStorage, чтобы видеть их типы.
// ============================================================
class SerialBridge {
  private:
    String _lineBuffer;
    unsigned long _lastStatusSend;
    ModuleControl *_radio;
    DataStorage *_storage;

    void sendEvent(const __FlashStringHelper *code) {
      Serial.print(F("EVT,"));
      Serial.print(code);
      Serial.println();
    }

    void sendEventFreq(const __FlashStringHelper *code, float freq) {
      Serial.print(F("EVT,"));
      Serial.print(code);
      Serial.print(',');
      Serial.println(freq, 1);
    }

    void sendError(const __FlashStringHelper *msg) {
      Serial.print(F("ERR,"));
      Serial.println(msg);
    }

    void sendStatus() {
      if (!_radio) return;
      // TEA5767 отдаёт RSSI 0..15 — пропорционально приводим к 0..10 для WPF
      int raw = _radio->getSignalLevel();
      int sig = (raw * RSSI_MAX_LEVEL + 7) / 15;
      if (sig > RSSI_MAX_LEVEL) sig = RSSI_MAX_LEVEL;
      if (sig < 0) sig = 0;
      Serial.print(_radio->getFrequency(), 1);
      Serial.print(',');
      Serial.print(sig);
      Serial.print(',');
      Serial.println(_radio->getStereo() ? '1' : '0');
    }

    void handleCommand(const String &raw) {
      String cmd = raw;
      cmd.trim();
      cmd.toUpperCase();
      if (cmd.length() == 0) return;
      if (!_radio) return;

      if (cmd == F("UP")) {
        _radio->stepUp();
        sendEvent(F("UP"));
      }
      else if (cmd == F("DOWN")) {
        _radio->stepDown();
        sendEvent(F("DOWN"));
      }
      else if (cmd == F("SCAN")) {
        // Повторный SCAN во время поиска останавливает его
        if (_radio->isScanning()) {
          _radio->stopScan();
          sendEvent(F("SCAN_END"));
        } else {
          _radio->startScan();
          sendEvent(F("SCAN_START"));
        }
      }
      else if (cmd == F("SAVE")) {
        if (_storage) {
          _storage->saveLastStation(_radio->getFrequency());
          sendEvent(F("SAVED"));
        }
      }
      else if (cmd == F("LOAD")) {
        if (_storage) {
          float f = _storage->loadLastStation();
          _radio->setFrequency(f);
          sendEventFreq(F("LOADED"), f);
        }
      }
      else if (cmd.startsWith(F("SET"))) {
        String arg = cmd.substring(3);
        arg.trim();
        // Разделитель после SET может быть пробелом или отсутствовать у «SET101.5»
        if (arg.length() == 0) { sendError(F("INVALID_FREQ")); return; }
        float f = arg.toFloat();
        if (isnan(f) || f < 76.0 || f > 108.0) {
          sendError(F("INVALID_FREQ"));
        } else {
          _radio->setFrequency(f);
          sendEventFreq(F("SET"), f);
        }
      }
      else if (cmd == F("STATUS")) {
        sendStatus();
      }
      else {
        sendError(F("UNKNOWN_CMD"));
      }
    }

  public:
    SerialBridge() : _lineBuffer(""), _lastStatusSend(0), _radio(nullptr), _storage(nullptr) {}

    void setRefs(ModuleControl *radio, DataStorage *storage) {
      _radio = radio;
      _storage = storage;
    }

    // Приём команд из Serial — вызывать каждый цикл
    void pollInput() {
      while (Serial.available() > 0) {
        char c = (char)Serial.read();
        if (c == '\n') {
          handleCommand(_lineBuffer);
          _lineBuffer = "";
        }
        else if (c == '\r') {
          // Пропускаем: CR+LF от WPF обработается на '\n'
        }
        else {
          _lineBuffer += c;
          if (_lineBuffer.length() > 32) _lineBuffer = ""; // защита от мусора
        }
      }
    }

    // Периодическая отправка статуса — вызывать каждый цикл
    void pollOutput() {
      if (millis() - _lastStatusSend >= STATUS_INTERVAL_MS) {
        _lastStatusSend = millis();
        sendStatus();
      }
    }

    // Сообщает WPF результат шага автопоиска
    void reportScanResult(int result) {
      if (result == 1)      sendEventFreq(F("SCAN_END"), _radio->getFrequency());
      else if (result == -1) {
        sendEvent(F("SCAN_NONE"));
        sendEvent(F("SCAN_END"));
      }
    }

    void sendStatusNow() { sendStatus(); }
};

class IrReceiverModel {
  private: int irPin;
  public:
    IrReceiverModel(int pin) { irPin = pin; }
    void begin() { IrReceiver.begin(irPin, ENABLE_LED_FEEDBACK); }
    int readCommand() {
      if (IrReceiver.decode()) {
        int cmd = IrReceiver.decodedIRData.command;
        IrReceiver.resume();
        return cmd;
      }
      return 0;
    }
};

class LcdView {
  private: LiquidCrystal_I2C lcd;
  public:
    LcdView() : lcd(0x27, 16, 2) { }
    void begin() { lcd.init(); lcd.backlight(); lcd.clear(); }
    void displayStatus(float freq, int sigLevel, bool isStereo) {
      lcd.setCursor(0, 0);
      lcd.print("Freq: "); lcd.print(freq, 1); lcd.print(" MHz   ");
      lcd.setCursor(0, 1);
      lcd.print("Sig: "); lcd.print(sigLevel); lcd.print(isStereo ? " ST   " : " MO   ");
    }
};

class MainController {
  private:
    ModuleControl *_radio;
    DataStorage *_storage;
    LcdView *_view;
    IrReceiverModel *_ir;
    SerialBridge _bridge;

    void updateView() {
      if (_view && _radio) _view->displayStatus(_radio->getFrequency(), _radio->getSignalLevel(), _radio->getStereo());
    }

  public:
    MainController(ModuleControl *radio, DataStorage *storage, LcdView *view, IrReceiverModel *ir)
      : _radio(radio), _storage(storage), _view(view), _ir(ir), _bridge() { }

    void begin() {
      if (_view) _view->begin();
      if (_ir) _ir->begin();
      if (_radio) {
        _radio->begin();
        float savedFreq = (_storage) ? _storage->loadLastStation() : 102.0;
        _radio->setFrequency(savedFreq);
        _radio->readStatus();
      }
      if (_radio) _bridge.setRefs(_radio, _storage);
      updateView();
    }

    void service() {
      // 1. Приём команд от WPF
      _bridge.pollInput();

      // 2. Автопоиск (SCAN) — non-blocking, один шаг за цикл
      if (_radio->isScanning()) {
        int result = _radio->scanStep();
        if (result != 0) {
          _bridge.reportScanResult(result);
        }
      }

      // 3. ИК-управление (пульт)
      if (_ir && _radio) {
        int code = _ir->readCommand();
        if (code != 0) {
          float freq = _radio->getFrequency();
          bool changed = false;
          switch (code) {
            case 0x47: if (freq < 108.0) { freq += 0.1; changed = true; } break;
            case 0x45: if (freq > 76.0)  { freq -= 0.1; changed = true; } break;
            case 0x46: _radio->nextStation(); break;
            case 0x09: if (_storage) _storage->saveLastStation(freq); break;
            case 0x43: if (_storage) { freq = _storage->loadLastStation(); changed = true; } break;
          }
          if (changed) _radio->setFrequency(freq);
        }
      }
      
      // 4. Чтение статуса TEA5767 и watchdog
      if (_radio) {
        _radio->readStatus();
        _radio->checkSignalWatchdog();
      }

      // 5. Периодическая отправка статуса в WPF
      _bridge.pollOutput();

      updateView();
    }
};

// Точка входа
ModuleControl radio;
IrReceiverModel ir(2);
DataStorage storage;
LcdView lcdView;
MainController controller(&radio, &storage, &lcdView, &ir);

void setup() {
  Serial.begin(SERIAL_BAUD);
  controller.begin();
}

void loop() {
  controller.service();
  // 100 мс: достаточно для LCD, но даёт быстрым командам и автопоиску отзывчивость
  delay(100);
}
