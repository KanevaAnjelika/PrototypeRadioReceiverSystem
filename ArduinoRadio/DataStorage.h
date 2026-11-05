#ifndef DATASTORAGE_H
#define DATASTORAGE_H

#include <Arduino.h>
#include <EEPROM.h>

/*
 * DataStorage - слой Model: хранение последней станции в EEPROM.
 *
 * ПРИЧИНА ПРЕЖНЕГО БАГА (0.0/NAN на экране):
 * в непрошитой EEPROM все байты = 0xFF. Битовая последовательность
 * 0xFF 0xFF 0xFF 0xFF для типа float - это NaN.
 * Проверка "if (freq < 76.0 || freq > 108.0)" NaN НЕ отсеивает,
 * потому что любое сравнение с NaN возвращает false.
 * NaN проходил валидацию -> попадал в ModuleControl -> (int)NaN = 0 -> "Freq: 0.0".
 *
 * Исправление:
 *   1) сначала магическое число (было ли сохранение вообще);
 *   2) затем проверка isnan (NaN != NaN - всегда true);
 *   3) затем диапазон;
 *   4) наружу отдаётся целое число десятых МГц (без float).
 */

#define DS_FREQ_DEFAULT_TENTHS 1020     // 102.0 МГц - частота по умолчанию
#define DS_FREQ_MIN_TENTHS      760     // 76.0 МГц
#define DS_FREQ_MAX_TENTHS     1080     // 108.0 МГц

class DataStorage {
  private:
    static const int MAGIC_ADDRESS = 0;      // признак заполненности EEPROM
    static const int FREQ_ADDRESS  = 4;      // адрес хранимой частоты
    static const uint8_t MAGIC_VALUE = 0xA5;

  public:
    // Сохраняет частоту (МГц) и ставит магическое число
    void saveLastStation(float freq) {
      if (isnan(freq)) return;
      saveLastStationTenths(toTenths(freq));
    }

    // Сохранение в десятых МГц (102.0 -> 1020)
    void saveLastStationTenths(uint16_t tenths) {
      uint16_t value = clamp(tenths);
      EEPROM.put(FREQ_ADDRESS, value);
      EEPROM.write(MAGIC_ADDRESS, MAGIC_VALUE);
    }

    // Загрузка в десятых МГц; при чистой EEPROM - 1020
    uint16_t loadLastStationTenths() {
      if (EEPROM.read(MAGIC_ADDRESS) != MAGIC_VALUE) {
        return DS_FREQ_DEFAULT_TENTHS;       // EEPROM чистая - первый запуск
      }

      uint16_t stored = 0;
      EEPROM.get(FREQ_ADDRESS, stored);

      if (!isKnownStation(stored)) {
        return DS_FREQ_DEFAULT_TENTHS;       // повреждённые данные
      }
      return stored;
    }

    // Совместимость со старым кодом (возвращает float)
    float loadLastStation() {
      return loadLastStationTenths() / 10.0f;
    }

    // Валидация float: NaN отсеивается отдельно (NaN < 76.0 == false!)
    static bool isValidFrequency(float freq) {
      if (isnan(freq)) return false;
      return (freq * 10.0f) >= (float)DS_FREQ_MIN_TENTHS &&
             (freq * 10.0f) <= (float)DS_FREQ_MAX_TENTHS;
    }

    // Валидация uint16_t (десятые МГц)
    static bool isKnownStation(uint16_t tenths) {
      return tenths >= DS_FREQ_MIN_TENTHS && tenths <= DS_FREQ_MAX_TENTHS;
    }

    // МГц -> десятые МГц с округлением (101.59999 -> 1016)
    static uint16_t toTenths(float freq) {
      if (isnan(freq)) return DS_FREQ_DEFAULT_TENTHS;
      float scaled = freq * 10.0f;
      long rounded = (long)(scaled + (scaled >= 0.0f ? 0.5f : -0.5f));
      if (rounded < DS_FREQ_MIN_TENTHS) rounded = DS_FREQ_MIN_TENTHS;
      if (rounded > DS_FREQ_MAX_TENTHS) rounded = DS_FREQ_MAX_TENTHS;
      return (uint16_t)rounded;
    }

    static uint16_t clamp(uint16_t tenths) {
      if (tenths < DS_FREQ_MIN_TENTHS) return DS_FREQ_MIN_TENTHS;
      if (tenths > DS_FREQ_MAX_TENTHS) return DS_FREQ_MAX_TENTHS;
      return tenths;
    }
};

#endif
