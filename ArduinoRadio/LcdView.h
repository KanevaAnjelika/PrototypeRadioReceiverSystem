#ifndef LCDVIEW_H
#define LCDVIEW_H

#include <Arduino.h>
#include <LiquidCrystal_I2C.h>

/*
 * LcdView - слой View: экран 1602 по I2C.
 *
 * Что исправлено:
 *  1) Частота больше НЕ передаётся в lcd.print() как float.
 *     Причина "Freq: 0.0 / NAN" - как раз lcd.print(float): библиотека
 *     LiquidCrystal_I2C форматирует float через dtostrf, и если значение
 *     равно NaN или пришло "кривым" float, на экране 0.0 либо nan.
 *     Теперь представление принимает ЦЕЛОЕ число десятых МГц (1020) и само
 *     разбирает его на целую и дробную часть: 1020 -> "102" и "0".
 *  2) Дробная часть всегда печатается одной цифрой, поэтому "102.0" не
 *     превращается в "102" или "102.00".
 *  3) Строка дополняется пробелами до 16 символов, чтобы остатки прежнего
 *     текста не искажали показ (было "102.0 MHz   " поверх "101.5 MHz").
 */

#define LCD_ADDRESS        0x27     // адрес LCD 1602 (если не работает - 0x3F)
#define LCD_COLUMNS          16
#define LCD_ROWS              2
#define LCD_DEFAULT_TENTHS 1020     // 102.0 МГц - что показать, если данных нет

class LcdView {
  private:
    LiquidCrystal_I2C lcd;

    // Печатает целое число десятых МГц как "102.0" - только целая арифметика
    void printFrequency(uint16_t tenths) {
      uint16_t integerPart = tenths / 10;     // 1020 -> 102
      uint8_t  decimalPart = (uint8_t)(tenths % 10);   // 1020 -> 0

      lcd.print((int)integerPart);
      lcd.print('.');
      lcd.print((int)decimalPart);
    }

  public:
    LcdView() : lcd(LCD_ADDRESS, LCD_COLUMNS, LCD_ROWS) { }

    void init() {
      lcd.init();
      lcd.backlight();
      lcd.clear();
      // Сразу показываем заведомо корректную частоту, чтобы на экране
      // не осталось 0.0 до первой настройки тюнера
      displayFrequency(LCD_DEFAULT_TENTHS);
    }

    // Основной способ вывода: частота в десятых МГц (1020 = 102.0 МГц)
    void displayFrequency(uint16_t tenths) {
      if (tenths < 100) tenths = LCD_DEFAULT_TENTHS;   // защита от "пустого" значения

      lcd.setCursor(0, 0);
      lcd.print("Freq: ");
      printFrequency(tenths);
      lcd.print(" MHz");
      padToColumn(13);
    }

    // Совместимость со старым кодом: принимаем float, но НЕ отдаём его в lcd.print.
    // Сначала переводим в целые десятые с проверкой NaN.
    void displayFrequencyFloat(float freq) {
      displayFrequency(toTenths(freq));
    }

    // Вторая строка: уровень сигнала и режим приёма
    void displaySignal(int level, bool isStereo) {
      if (level < 0) level = 0;
      if (level > 31) level = 31;

      lcd.setCursor(0, 1);
      lcd.print("Sig: ");
      lcd.print(level);
      lcd.print(isStereo ? " ST" : " MO");
      padToColumn(12);
    }

    // Обе строки сразу - этот метод вызывает MainController::updateView()
    void displayStatus(uint16_t tenths, int level, bool isStereo) {
      displayFrequency(tenths);
      displaySignal(level, isStereo);
    }

    // Тот же вывод, но частота приходит как float
    void displayStatusFloat(float freq, int level, bool isStereo) {
      displayStatus(toTenths(freq), level, isStereo);
    }

    // Служебное сообщение на всю первую строку (например, при поиске)
    void displayMessage(const char *text) {
      lcd.setCursor(0, 0);
      lcd.print(text);
      padToColumn((uint8_t)(textLength(text) + 1));
    }

    // МГц -> десятые МГц с округлением и отбраковкой NaN (тот же приём, что в DataStorage)
    static uint16_t toTenths(float freq) {
      if (isnan(freq)) return LCD_DEFAULT_TENTHS;
      float scaled = freq * 10.0f;
      long rounded = (long)(scaled + (scaled >= 0.0f ? 0.5f : -0.5f));
      if (rounded < 100) return LCD_DEFAULT_TENTHS;
      if (rounded > 65535) return LCD_DEFAULT_TENTHS;
      return (uint16_t)rounded;
    }

  private:
    // Длина строки без string.h
    static uint8_t textLength(const char *text) {
      uint8_t length = 0;
      while (text[length] != '\0') length++;
      return length;
    }

    // Добивает текущую строку пробелами до 16 символов
    void padToColumn(uint8_t column) {
      for (uint8_t i = column; i < LCD_COLUMNS; i++) {
        lcd.print(' ');
      }
    }
};

#endif