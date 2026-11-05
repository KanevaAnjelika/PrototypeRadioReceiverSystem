#ifndef MAINCONTROLLER_H
#define MAINCONTROLLER_H

#include <Arduino.h>

#include "ModuleControl.h"
#include "DataStorage.h"
#include "LcdView.h"
#include "IrReceiverModel.h"

/*
 * MainController - слой Controller: связывает модели и представление.
 *
 * КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ.
 * Раньше контроллер был пустой заглушкой, а частота хранилась ДВАЖДЫ:
 * в глобальной переменной currentFreq внутри скетча и в поле
 * currentFrequency внутри ModuleControl. В loop() на экран выводилось
 * значение из модуля, а команды меняли глобальную переменную - из-за этого
 * экран мог показывать 0.0/устаревшее значение, а TEA5767 - другую частоту.
 *
 * Теперь единственный источник правды - ModuleControl. Контроллер читает
 * частоту из модели (getFrequencyTenths) и передаёт её в представление
 * (LcdView::displayStatus), поэтому значение не теряется.
 */

// Коды кнопок пульта (можно переопределить в скетче до подключения заголовка)
#ifndef MC_IR_CODE_UP
#define MC_IR_CODE_UP   0x0D
#endif
#ifndef MC_IR_CODE_DOWN
#define MC_IR_CODE_DOWN 0x08
#endif
#ifndef MC_IR_CODE_SCAN
#define MC_IR_CODE_SCAN 0x5A
#endif
#ifndef MC_IR_CODE_SAVE
#define MC_IR_CODE_SAVE 0x40
#endif

class MainController {
  private:
    ModuleControl   *_radio;    // модель FM-тюнера (единственный источник частоты)
    DataStorage     *_storage;  // модель хранилища EEPROM
    LcdView         *_view;     // представление LCD
    IrReceiverModel *_ir;       // модель ИК-приёмника

  public:
    MainController(ModuleControl *radio, DataStorage *storage,
                   LcdView *view, IrReceiverModel *ir)
      : _radio(radio), _storage(storage), _view(view), _ir(ir) {
    }

    // Инициализация - вызывается один раз из setup()
    void begin() {
      if (_radio != NULL)  _radio->begin();       // Wire.begin()
      if (_view != NULL)   _view->init();         // lcd.init + backlight
      if (_ir != NULL)     _ir->init();           // IrReceiver.begin

      // 1. достаём из хранилища ЦЕЛОЕ число десятых МГц (1020 = 102.0 МГц);
      //    при пустой EEPROM DataStorage вернёт 1020, а не NaN
      uint16_t tenths = (_storage != NULL && _radio != NULL)
                          ? _storage->loadLastStationTenths()
                          : (uint16_t)MC_TENTHS_DEFAULT;

      // 2. настраиваем TEA5767 на эту частоту
      if (_radio != NULL) _radio->setFrequencyTenths(tenths);

      // 3. сразу показываем её на экране, не дожидаясь первого loop()
      updateView();
    }

    // Обработка команды с ИК-пульта
    void processIrCommand() {
      if (_ir == NULL || _radio == NULL) return;

      int code = _ir->readCommand();
      if (code == 0) return;                 // ничего не принято

      switch (code) {
        case MC_IR_CODE_UP:
          _radio->stepUp();
          break;
        case MC_IR_CODE_DOWN:
          _radio->stepDown();
          break;
        case MC_IR_CODE_SAVE:
          if (_storage != NULL) {
            _storage->saveLastStationTenths(_radio->getFrequencyTenths());
          }
          break;
        case MC_IR_CODE_SCAN:
          // TODO: автопоиск станции (логика будет добавлена отдельным методом)
          break;
        default:
          return;                            // посторонние коды игнорируем
      }

      updateView();
    }

    // Обновление представления: модель -> LcdView.
    // Частота передаётся целым числом десятых МГц, поэтому lcd.print
    // никогда не получает float и не может напечатать 0.0 или nan.
    void updateView() {
      if (_view == NULL || _radio == NULL) return;

      _view->displayStatus(_radio->getFrequencyTenths(),
                           _radio->getSignalLevel(),
                           _radio->getStereo());
    }

    // Полный цикл обслуживания: опрос пульта, чтение состояния тюнера, вывод
    void service() {
      processIrCommand();
      if (_radio != NULL) _radio->readStatus();
      updateView();
    }

    // Текстовая команда (например, из COM-порта)
    void handleCommand(const char *command) {
      if (command == NULL || _radio == NULL) return;

      if (strcmp(command, "UP") == 0) {
        _radio->stepUp();
      } else if (strcmp(command, "DOWN") == 0) {
        _radio->stepDown();
      } else if (strcmp(command, "SAVE") == 0) {
        if (_storage != NULL) {
          _storage->saveLastStationTenths(_radio->getFrequencyTenths());
        }
      } else if (strcmp(command, "SCAN") == 0) {
        // TODO: автопоиск станции
      } else {
        return;
      }

      updateView();
    }

    // Текущая частота в десятых МГц - для отчёта в COM-порт
    uint16_t frequencyTenths() {
      return (_radio != NULL) ? _radio->getFrequencyTenths() : (uint16_t)MC_TENTHS_DEFAULT;
    }

    // Текущая частота в МГц - для Serial.print(freq, 1)
    float frequency() {
      return (_radio != NULL) ? _radio->getFrequency() : (MC_TENTHS_DEFAULT / 10.0f);
    }

    int signalLevel() { return (_radio != NULL) ? _radio->getSignalLevel() : 0; }
    bool isStereo()   { return (_radio != NULL) ? _radio->getStereo() : false; }
};

#endif
