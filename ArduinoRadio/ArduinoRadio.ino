#include <Wire.h>
#include <LiquidCrystal_I2C.h>
#include <IRremote.hpp>
#include <EEPROM.h>

// ==========================================
// 1. МОДЕЛИ (Model)
// ==========================================

// ---------- Модель EEPROM ----------
class DataStorage {
  private:
    const int MAGIC_ADDR = 0;
    const int FREQ_ADDR  = 4;
    const uint8_t MAGIC_VALUE = 0xA5;

  public:
    void saveLastStation(float freq) {
      if (isnan(freq)) return;
      int tenths = (int)(freq * 10.0 + 0.5);
      if (tenths < 760)  tenths = 760;
      if (tenths > 1080) tenths = 1080;
      
      EEPROM.put(FREQ_ADDR, (uint16_t)tenths);
      EEPROM.write(MAGIC_ADDR, MAGIC_VALUE);
      Serial.print("SAVED to EEPROM: ");
      Serial.println(freq, 1);
    }

    float loadLastStation() {
      if (EEPROM.read(MAGIC_ADDR) != MAGIC_VALUE) {
        Serial.println("EEPROM empty — default 102.0");
        return 102.0;
      }
      uint16_t tenths = 0;
      EEPROM.get(FREQ_ADDR, tenths);
      if (tenths < 760 || tenths > 1080) {
        Serial.println("EEPROM corrupt — default 102.0");
        return 102.0;
      }
      float freq = tenths / 10.0;
      Serial.print("LOADED from EEPROM: ");
      Serial.println(freq, 1);
      return freq;
    }
};

// ---------- Модель TEA5767 ----------
class ModuleControl {
  private:
    int      _signalLevel;
    bool     _isStereo;
    float    _currentFreq;
    int      _currentIndex;
    const int TEA5767_ADDR = 0x60;

    // Список станций (Кировск)
    const float _stations[3] = {102.0, 102.5, 103.5};
    const int   _stationCount = 3;

  public:
    ModuleControl() {
      _signalLevel = 0;
      _isStereo    = false;
      _currentFreq = 102.0;
      _currentIndex = 0;
    }

    void begin() {
      Wire.begin();
    }

    void setFrequency(float freq) {
      if (isnan(freq)) return;
      if (freq < 76.0) freq = 76.0;
      if (freq > 108.0) freq = 108.0;
      
      _currentFreq = freq;
      
      // Рабочая формула PLL
      unsigned int frequencyB = 4 * (freq * 1000000 + 225000) / 32768;
      byte frequencyH = frequencyB >> 8;
      byte frequencyL = frequencyB & 0xFF;

      Wire.beginTransmission(TEA5767_ADDR);
      Wire.write(frequencyH);
      Wire.write(frequencyL);
      Wire.write(0xB0);
      Wire.write(0x10);
      Wire.write(0x00);
      Wire.endTransmission();

      delay(120); // ВАЖНО: даем PLL стабилизироваться (убирает писк)
    }

    void stepUp()   { setFrequency(_currentFreq + 0.1); }
    void stepDown() { setFrequency(_currentFreq - 0.1); }

    void nextStation() {
      _currentIndex = (_currentIndex + 1) % _stationCount;
      setFrequency(_stations[_currentIndex]);
    }

    void readStatus() {
      Wire.requestFrom(TEA5767_ADDR, 5);
      if (Wire.available() >= 5) {
        Wire.read();
        Wire.read();
        byte byte3 = Wire.read();
        byte byte4 = Wire.read();
        Wire.read();

        _signalLevel = byte4 >> 4;
        _isStereo    = byte3 & 0x80;
      }
    }

    float getFrequency()   { return _currentFreq; }
    int   getSignalLevel() { return _signalLevel; }
    bool  getStereo()      { return _isStereo; }
};

// ---------- Модель ИК-приемника ----------
class IrReceiverModel {
  private:
    int irPin;

  public:
    IrReceiverModel(int pin) { irPin = pin; }

    void begin() {
      IrReceiver.begin(irPin, ENABLE_LED_FEEDBACK);
    }

    int readCommand() {
      if (IrReceiver.decode()) {
        int cmd = IrReceiver.decodedIRData.command;
        IrReceiver.resume();
        return cmd;
      }
      return 0;
    }
};

// ==========================================
// 2. ПРЕДСТАВЛЕНИЕ (View)
// ==========================================

class LcdView {
  private:
    LiquidCrystal_I2C lcd;

  public:
    LcdView() : lcd(0x27, 16, 2) { }

    void begin() {
      lcd.init();
      lcd.backlight();
      lcd.clear();
    }

    void displayStatus(float freq, int sigLevel, bool isStereo) {
      lcd.setCursor(0, 0);
      lcd.print("Freq: ");
      lcd.print(freq, 1);
      lcd.print(" MHz   ");

      lcd.setCursor(0, 1);
      lcd.print("Sig: ");
      lcd.print(sigLevel);
      lcd.print(isStereo ? " ST   " : " MO   ");
    }
};

// ==========================================
// 3. КОНТРОЛЛЕР (Controller)
// ==========================================

class MainController {
  private:
    ModuleControl   *_radio;
    DataStorage     *_storage;
    LcdView         *_view;
    IrReceiverModel *_ir;

    void updateView() {
      if (_view != NULL && _radio != NULL) {
        _view->displayStatus(_radio->getFrequency(), 
                             _radio->getSignalLevel(), 
                             _radio->getStereo());
      }
    }

  public:
    MainController(ModuleControl *radio, DataStorage *storage, LcdView *view, IrReceiverModel *ir)
      : _radio(radio), _storage(storage), _view(view), _ir(ir) { }

    void begin() {
      // Порядок как в монолите
      if (_view != NULL)  _view->begin();
      if (_ir != NULL)    _ir->begin();
      if (_radio != NULL) _radio->begin();

      Serial.println("Start TEA5767 + IR + EEPROM + COM...");

      float savedFreq = (_storage != NULL) ? _storage->loadLastStation() : 102.0;
      if (_radio != NULL) {
        _radio->setFrequency(savedFreq);
        _radio->readStatus();
      }
      
      updateView();
    }

    void service() {
      // 1. Обработка ИК
      if (_ir != NULL && _radio != NULL) {
        int code = _ir->readCommand();
        if (code != 0) {
          float freq = _radio->getFrequency();
          bool changed = false;

          switch (code) {
            case 0x47: if (freq < 108.0) { freq += 0.1; changed = true; } break;  // CH+
            case 0x45: if (freq > 76.0)  { freq -= 0.1; changed = true; } break;  // CH-
            case 0x40: if (freq < 107.1) { freq += 1.0; changed = true; } break;  // ⏩
            case 0x44: if (freq > 76.9)  { freq -= 1.0; changed = true; } break;  // ⏪
            
            case 0x46:                                                            // CH — автопоиск
              _radio->nextStation(); 
              break;

            case 0x09:                                                            // EQ — СОХРАНИТЬ
              if (_storage != NULL) _storage->saveLastStation(freq);
              break;

            case 0x43:                                                            // ▶|| — ЗАГРУЗИТЬ
              if (_storage != NULL) {
                freq = _storage->loadLastStation();
                changed = true;
              }
              break;

            default: break;
          }

          if (changed) {
            _radio->setFrequency(freq);
          }
        }
      }

      // 2. Чтение статуса (ТОЛЬКО ЗДЕСЬ, чтобы не спамить шину)
      if (_radio != NULL) _radio->readStatus();

      // 3. Обновление экрана
      updateView();

      // 4. Вывод в COM
      if (_radio != NULL) {
        Serial.print("Freq: "); Serial.print(_radio->getFrequency(), 1);
        Serial.print(" | Sig: "); Serial.print(_radio->getSignalLevel());
        Serial.print(" | Stereo: "); Serial.println(_radio->getStereo() ? "YES" : "NO");
      }
    }

    void handleSerialCommand() {
      if (!Serial.available()) return;
      String cmd = Serial.readStringUntil('\n');
      cmd.trim();
      if (cmd.length() == 0) return;

      Serial.print("CMD: ");
      Serial.println(cmd);

      if (_radio == NULL) return;
      float freq = _radio->getFrequency();

      if (cmd == "UP") {
        if (freq < 108.0) _radio->setFrequency(freq + 0.1);
      } else if (cmd == "DOWN") {
        if (freq > 76.0) _radio->setFrequency(freq - 0.1);
      } else if (cmd == "NEXT") {
        _radio->nextStation();
      } else if (cmd == "SAVE") {
        if (_storage != NULL) _storage->saveLastStation(freq);
      } else if (cmd == "LOAD") {
        if (_storage != NULL) _radio->setFrequency(_storage->loadLastStation());
      } else if (cmd.startsWith("FREQ=")) {
        float f = cmd.substring(5).toFloat();
        if (f >= 76.0 && f <= 108.0) _radio->setFrequency(f);
        else Serial.println("FREQ out of range 76..108");
      } else if (cmd == "STATUS") {
        Serial.print("STATUS Freq="); Serial.print(freq, 1);
        Serial.print(" Sig="); Serial.print(_radio->getSignalLevel());
        Serial.print(" Stereo="); Serial.println(_radio->getStereo() ? "YES" : "NO");
      } else {
        Serial.println("Unknown command");
      }
    }
};

// ==========================================
// 4. ТОЧКА ВХОДА (Arduino)
// ==========================================

ModuleControl   radio;
IrReceiverModel ir(2);
DataStorage     storage;
LcdView         lcdView;

MainController controller(&radio, &storage, &lcdView, &ir);

void setup() {
  Serial.begin(9600);
  controller.begin();
}

void loop() {
  controller.service();
  controller.handleSerialCommand();
  delay(300); // Задержка из монолита, чтобы шина I2C и PLL не захлебывались
}