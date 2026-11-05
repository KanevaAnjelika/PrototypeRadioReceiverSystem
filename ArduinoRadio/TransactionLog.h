#ifndef TRANSACTIONLOG_H
#define TRANSACTIONLOG_H

#include <Arduino.h>

class TransactionLog {
  private:
    String logBuffer;

  public:
    void addEvent(String text) {
      logBuffer += text + "\n";
      Serial.println(text);
    }

    String getLog() {
      return logBuffer;
    }

    void clearLog() {
      logBuffer = "";
    }
};

#endif
