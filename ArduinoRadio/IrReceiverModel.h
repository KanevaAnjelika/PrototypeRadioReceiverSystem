#ifndef IRRECEIVERMODEL_H
#define IRRECEIVERMODEL_H

#include <Arduino.h>
// Специальный макрос, чтобы избежать ошибки multiple definition
#define USE_IRREMOTE_HPP_AS_PLAIN_INCLUDE 
#include <IRremote.h>

class IrReceiverModel {
  private:
    int irPin;
  public:
    IrReceiverModel(int pin) { 
      irPin = pin; 
    }
    
    void init() {
      IrReceiver.begin(irPin, ENABLE_LED_FEEDBACK);
    }
    
    int readCommand() {
      if (IrReceiver.decode()) {
        int command = IrReceiver.decodedIRData.command;
        IrReceiver.resume();
        return command;
      }
      return 0;
    }
};

#endif
