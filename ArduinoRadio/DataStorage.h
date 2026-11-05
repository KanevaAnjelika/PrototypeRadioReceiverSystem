#ifndef DATASTORAGE_H
#define DATASTORAGE_H

#include <EEPROM.h>

class DataStorage {
  public:
    void saveLastStation(float freq) {
      EEPROM.put(0, freq);
    }
    
    float loadLastStation() {
      float freq;
      EEPROM.get(0, freq);
      if (freq < 76.0 || freq > 108.0) {
        return 101.5;
      }
      return freq;
    }
};

#endif