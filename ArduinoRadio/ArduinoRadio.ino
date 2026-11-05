#include <Arduino.h>
#include <IRremote.hpp> 

#include "ModuleControl.h"
#include "DataStorage.h"
#include "LcdView.h"

class IrReceiverModel {
  private:
    int irPin;
  public:
    IrReceiverModel(int pin) { irPin = pin; }
    void init() { IrReceiver.begin(irPin, ENABLE_LED_FEEDBACK); }
    int readCommand() {
      if (IrReceiver.decode()) {
        int command = IrReceiver.decodedIRData.command;
        IrReceiver.resume();
        return command;
      }
      return 0;
    }
};

ModuleControl radio;
IrReceiverModel ir(2); 
DataStorage storage;
LcdView lcd;

float currentFreq = 101.5;

void setup() {
  Serial.begin(9600);
  lcd.init();
  ir.init();
  
  currentFreq = storage.loadLastStation();
  radio.setFrequency(currentFreq);
  delay(1000);
}

void loop() {
  int command = ir.readCommand();
  
  if (command != 0) {
    if (command == 0x0D) { 
      currentFreq += 0.1;
      if (currentFreq > 108.0) currentFreq = 76.0;
      radio.setFrequency(currentFreq);
    } 
    else if (command == 0x08) { 
      currentFreq -= 0.1;
      if (currentFreq < 76.0) currentFreq = 108.0;
      radio.setFrequency(currentFreq);
    }
  }

  radio.readStatus();
  
  // Разделяем число на целую и дробную часть
  float f = radio.getFrequency();
  int integerPart = (int)f;
  int decimalPart = (int)((f - integerPart) * 10);
  if (decimalPart < 0) decimalPart = -decimalPart; // на случай отрицательных значений

  // Выводим вручную в LcdView
  lcd.displayDataManual(integerPart, decimalPart, radio.getSignalLevel(), radio.getStereo());

  delay(200); 
}