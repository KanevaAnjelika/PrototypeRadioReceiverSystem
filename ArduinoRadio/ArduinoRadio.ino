/*
 * ArduinoRadio.ino
 *
 * FM-приёмник на TEA5767 + ИК-пульт + LCD 1602 (I2C).
 * Скетч тонкий: вся логика разнесена по слоям MVC
 * (ModuleControl / DataStorage / IrReceiverModel - модели,
 *  LcdView - представление, MainController - контроллер).
 *
 * ВАЖНЫЕ ИЗМЕНЕНИЯ (почему раньше было "Freq: 0.0"):
 *  1) Класс IrReceiverModel был продублирован прямо в скетче - теперь
 *     используется единственный вариант из IrReceiverModel.h.
 *  2) Исчезла глобальная переменная currentFreq: частота хранится только
 *     в ModuleControl и оттуда же попадает на экран через MainController,
 *     поэтому значение не может "потеряться" между слоями.
 *  3) DataStorage больше не возвращает NaN из чистой EEPROM (см. DataStorage.h).
 *  4) В LcdView частота уходит целым числом десятых МГц, а не float,
 *     поэтому lcd.print не печатает 0.0 / nan.
 */

#include <Arduino.h>

#include "ModuleControl.h"
#include "IrReceiverModel.h"
#include "DataStorage.h"
#include "LcdView.h"
#include "MainController.h"

// ==================== ОБЪЕКТЫ MVC ====================

ModuleControl   radio;            // модель TEA5767
IrReceiverModel ir(2);            // модель ИК-приёмника, пин D2
DataStorage     storage;          // модель хранилища (EEPROM)
LcdView         lcdView;          // представление LCD 1602

// Контроллер связывает модели и представление
MainController controller(&radio, &storage, &lcdView, &ir);

// ==================== SETUP ====================

void setup() {
  Serial.begin(9600);

  // begin() сам: поднимает Wire, инициализирует LCD и ИК-приёмник,
  // загружает частоту из EEPROM (при пустой EEPROM - 102.0 МГц),
  // настраивает TEA5767 и выводит результат на экран
  controller.begin();
}

// ==================== LOOP ====================

void loop() {
  // Опрос пульта -> чтение RSSI/стерео -> обновление экрана.
  // Все значения берутся из ModuleControl, экран всегда показывает
  // ту же частоту, на которую реально настроен тюнер.
  controller.service();

  delay(200);
}
