#ifndef MODULECONTROL_H
#define MODULECONTROL_H

#include <Wire.h>
#include <Arduino.h>

class ModuleControl {
  private:
    int signalLevel;
    bool isStereo;
    float currentFrequency;
  public:
    ModuleControl() {
      signalLevel = 0;
      isStereo = false;
      currentFrequency = 102.0;
      Wire.begin();
    }
    
    void setFrequency(float freq) {
      currentFrequency = freq;
      unsigned int frequencyB = 4 * (freq * 1000000 + 225000) / 32768;
      uint8_t frequencyH = frequencyB >> 8;
      uint8_t frequencyL = frequencyB & 0XFF;

      Wire.beginTransmission(0x60);
      Wire.write(frequencyH);
      Wire.write(frequencyL);
      Wire.write(0xB0);
      Wire.write(0x10);
      Wire.write(0x00);
      Wire.endTransmission();
    }
    
    void readStatus() {
      Wire.requestFrom(0x60, 5);
      if (Wire.available() >= 5) {
        Wire.read(); 
        Wire.read(); 
        uint8_t byte3 = Wire.read(); 
        uint8_t byte4 = Wire.read(); 
        Wire.read(); 
        signalLevel = byte4 >> 4;
        isStereo = byte3 & 0x80;
      }
    }
    
    int getSignalLevel() { return signalLevel; }
    bool getStereo() { return isStereo; }
    float getFrequency() { return currentFrequency; }
};

#endif