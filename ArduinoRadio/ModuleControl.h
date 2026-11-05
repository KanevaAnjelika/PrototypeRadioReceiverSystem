#ifndef MODULECONTROL_H
#define MODULECONTROL_H

#include <Wire.h>
#include <Arduino.h>

/*
 * ModuleControl - слой Model: FM-модуль TEA5767 на адресе 0x60.
 *
 * Что исправлено:
 *  1) Частота хранится ЦЕЛЫМ числом десятых МГц (_frequencyTenths):
 *     102.0 МГц -> 1020. Это исключает ошибку float при шаге 0.1 МГц
 *     (101.5 + 0.1 + 0.1 ... в float даёт 101.79999 -> на экране "101.7")
 *     и делает невозможным вывод NaN.
 *  2) setFrequency() отбраковывает NaN и значения вне диапазона,
 *     поэтому в регистры чипа и на экран не может попасть 0.0/NAN.
 *  3) Wire.begin() убран из конструктора глобального объекта в begin():
 *     инициализация шины теперь происходит в setup(), как и положено.
 *  4) readStatus() сначала переводит TEA5767 в режим чтения (0x50),
 *     иначе Wire.requestFrom() не возвращает данных и "Sig:" всегда 0.
 */

#define MC_TEA5767_ADDRESS   0x60     // адрес TEA5767 на шине I2C
#define MC_TUNE_DELAY_MS       120    // время перестройки PLL после смены частоты
#define MC_TENTHS_MIN         760     // 76.0 МГц
#define MC_TENTHS_MAX        1080     // 108.0 МГц
#define MC_TENTHS_DEFAULT    1020     // 102.0 МГц - состояние по умолчанию

class ModuleControl {
  private:
    int      _signalLevel;        // RSSI 0..31
    bool     _isStereo;           // принимается стерео
    uint16_t _frequencyTenths;    // частота в десятых МГц: 1020 = 102.0 МГц
    bool     _tuned;              // модуль хотя бы раз настроен

    // Пересчёт десятых МГц в 12-битное слово PLL и запись в TEA5767.
    // Формат кадра TEA5767: 0x11, PLL(st.), PLL(мл.), конфиг, конфиг
    void tune() {
      uint16_t channel = _frequencyTenths;   // 102.0 МГц -> 1020

      Wire.beginTransmission(MC_TEA5767_ADDRESS);
      Wire.write((uint8_t)0x11);                     // старт записи, режим без поиска
      Wire.write(highByte(channel));                 // старшие биты слова PLL
      Wire.write(lowByte(channel));                  // младшие биты слова PLL
      Wire.write((uint8_t)0x00);                     // европейский диапазон, стерео
      Wire.write((uint8_t)0x00);                     // RDS выключен
      Wire.endTransmission();

      _tuned = true;
      delay(MC_TUNE_DELAY_MS);                       // даём PLL перестроиться
    }

  public:
    // В конструкторе шину не трогаем: объект глобальный, и Wire ещё не готов
    ModuleControl() {
      _signalLevel = 0;
      _isStereo = false;
      _frequencyTenths = MC_TENTHS_DEFAULT;
      _tuned = false;
    }

    // Инициализация шины I2C - вызывать из setup()
    void begin() {
      Wire.begin();
    }

    // Установка частоты из float с защитой от NaN и выхода за диапазон
    void setFrequency(float freq) {
      if (isnan(freq)) {
        return;                        // NaN игнорируем: прежняя частота остаётся
      }
      float scaled = freq * 10.0f;
      long tenths = (long)(scaled + (scaled >= 0.0f ? 0.5f : -0.5f));
      if (tenths < MC_TENTHS_MIN) tenths = MC_TENTHS_MIN;
      if (tenths > MC_TENTHS_MAX) tenths = MC_TENTHS_MAX;
      setFrequencyTenths((uint16_t)tenths);
    }

    // Установка частоты целым числом десятых МГц (основной путь, без float)
    void setFrequencyTenths(uint16_t tenths) {
      if (tenths < MC_TENTHS_MIN) tenths = MC_TENTHS_MIN;
      if (tenths > MC_TENTHS_MAX) tenths = MC_TENTHS_MAX;
      _frequencyTenths = tenths;
      tune();
    }

    // Шаг 0.1 МГц вверх/вниз - целочисленный, без накопления ошибки float
    void stepUp() {
      if (_frequencyTenths < MC_TENTHS_MAX) setFrequencyTenths(_frequencyTenths + 1);
    }

    void stepDown() {
      if (_frequencyTenths > MC_TENTHS_MIN) setFrequencyTenths(_frequencyTenths - 1);
    }

    // Частота в десятых МГц - именно её надо передавать в LcdView
    uint16_t getFrequencyTenths() { return _frequencyTenths; }

    // Частота в МГц - для расчётов и отчёта в COM-порт
    float getFrequency() { return _frequencyTenths / 10.0f; }

    // Чтение регистра состояния: RSSI и признак стерео
    void readStatus() {
      // TEA5767 переходит в режим чтения только после команды с установленным
      // битом 7 в первом байте; без этого requestFrom() возвращает пустоту
      Wire.beginTransmission(MC_TEA5767_ADDRESS);
      Wire.write((uint8_t)0x50);
      Wire.endTransmission(false);

      uint8_t count = Wire.requestFrom((uint8_t)MC_TEA5767_ADDRESS, (uint8_t)8);
      if (count < 8) {
        return;                        // модуль не ответил - оставляем прежние значения
      }

      uint8_t status[8];
      for (uint8_t i = 0; i < 8; i++) {
        status[i] = (uint8_t)Wire.read();
      }

      // Байт N4: биты 7..3 - RSSI (0..31), бит 2 - SMODE (1 = моно)
      _signalLevel = (status[4] >> 3) & 0x1F;
      _isStereo    = ((status[4] >> 2) & 0x01) == 0;
    }

    int  getSignalLevel() { return _signalLevel; }
    bool getStereo()      { return _isStereo; }
    bool isTuned()        { return _tuned; }
};

#endif