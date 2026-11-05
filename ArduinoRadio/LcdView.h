#ifndef LCDVIEW_H
#define LCDVIEW_H

#include <LiquidCrystal_I2C.h>

class LcdView {
  private:
    LiquidCrystal_I2C lcd;
  public:
    LcdView() : lcd(0x27, 16, 2) { }
    
    void init() {
      lcd.init();
      lcd.backlight();
      lcd.clear();
    }

    // Новый метод ручного вывода
    void displayDataManual(int intPart, int decPart, int level, bool isStereo) {
      lcd.setCursor(0, 0);
      lcd.print("Freq: ");
      lcd.print(intPart);     // Печатаем целую часть (например, 102)
      lcd.print(".");         // Точка
      lcd.print(decPart);     // Дробная часть (например, 0)
      lcd.print(" MHz   ");
      
      lcd.setCursor(0, 1);
      lcd.print("Sig: ");
      lcd.print(level);
      lcd.print(isStereo ? " ST   " : " MO   ");
    }
};

#endif