#ifndef SECONDCONTROLLER_H
#define SECONDCONTROLLER_H

#include <Arduino.h>

class SecondController {
  public:
    void sendData(String data) {
      // Отправка данных в COM-порт
    }

    String receiveCommand() {
      // Приём команды из COM-порта
      return "";
    }
};

#endif
