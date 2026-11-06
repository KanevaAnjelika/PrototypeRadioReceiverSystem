#include <Wire.h>
#include <LiquidCrystal_I2C.h>
#include <IRremote.hpp>
#include <EEPROM.h>

LiquidCrystal_I2C lcd(0x27, 16, 2);

#define TEA5767_ADDR 0x60
#define IR_PIN       2

// ---------- EEPROM ----------
#define EEPROM_MAGIC_ADDR  0
#define EEPROM_FREQ_ADDR   4
#define EEPROM_MAGIC_VALUE 0xA5

// ---------- Список станций ----------
const float stations[] = {
  // Кировск
  102.0,  // Радио России
  102.5,  // Народное радио Хибины
  103.5,  // Ретро FM / Кольцо Хибин
  // Апатиты
  //107.2, 105.7, 106.7, 103.1, 100.7,
  // Мурманск
  //97.2, 88.0, 107.4, 103.0, 104.5, 106.0, 100.6, 90.8,
  // Кандалакша
  //102.2
};
const int stationCount = sizeof(stations) / sizeof(stations[0]);
int       currentIndex = 0;

float currentFreq = stations[0];
int   signalLevel = 0;
bool  isStereo    = false;

// ---------- EEPROM ----------
void saveToEeprom(float freq) {
  if (isnan(freq)) return;
  int tenths = (int)(freq * 10.0 + 0.5);
  if (tenths < 760)  tenths = 760;
  if (tenths > 1080) tenths = 1080;
  EEPROM.put(EEPROM_FREQ_ADDR, (uint16_t)tenths);
  EEPROM.write(EEPROM_MAGIC_ADDR, EEPROM_MAGIC_VALUE);
  Serial.print("SAVED to EEPROM: ");
  Serial.println(freq, 1);
}

float loadFromEeprom() {
  if (EEPROM.read(EEPROM_MAGIC_ADDR) != EEPROM_MAGIC_VALUE) {
    Serial.println("EEPROM empty — default 102.0");
    return 102.0;
  }
  uint16_t tenths = 0;
  EEPROM.get(EEPROM_FREQ_ADDR, tenths);
  if (tenths < 760 || tenths > 1080) {
    Serial.println("EEPROM corrupt — default 102.0");
    return 102.0;
  }
  float freq = tenths / 10.0;
  Serial.print("LOADED from EEPROM: ");
  Serial.println(freq, 1);
  return freq;
}

// ---------- Установка частоты ----------
void setFrequency(float freq) {
  unsigned int frequencyB = 4 * (freq * 1000000 + 225000) / 32768;
  byte frequencyH = frequencyB >> 8;
  byte frequencyL = frequencyB & 0xFF;

  Wire.beginTransmission(TEA5767_ADDR);
  Wire.write(frequencyH);
  Wire.write(frequencyL);
  Wire.write(0xB0);
  Wire.write(0x10);
  Wire.write(0x00);
  Wire.endTransmission();
}

// ---------- Чтение статуса ----------
void readStatus() {
  Wire.requestFrom(TEA5767_ADDR, 5);
  if (Wire.available() >= 5) {
    Wire.read();
    Wire.read();
    byte byte3 = Wire.read();
    byte byte4 = Wire.read();
    Wire.read();

    signalLevel = byte4 >> 4;
    isStereo    = byte3 & 0x80;
  }
}

// ---------- Автопоиск ----------
void nextStation() {
  currentIndex = (currentIndex + 1) % stationCount;
  currentFreq  = stations[currentIndex];
  setFrequency(currentFreq);
  delay(200);
  readStatus();
  Serial.print("NEXT: ");
  Serial.print(currentFreq, 1);
  Serial.print(" Sig=");
  Serial.println(signalLevel);
}

// ---------- COM-команды (пригодятся, когда подключишь ВПФ) ----------
void handleSerialCommand() {
  if (!Serial.available()) return;
  String cmd = Serial.readStringUntil('\n');
  cmd.trim();
  if (cmd.length() == 0) return;

  Serial.print("CMD: ");
  Serial.println(cmd);

  if (cmd == "UP") {
    if (currentFreq < 108.0) { currentFreq += 0.1; setFrequency(currentFreq); delay(120); readStatus(); }
  } else if (cmd == "DOWN") {
    if (currentFreq > 76.0) { currentFreq -= 0.1; setFrequency(currentFreq); delay(120); readStatus(); }
  } else if (cmd == "NEXT") {
    nextStation();
  } else if (cmd == "SAVE") {
    saveToEeprom(currentFreq);
  } else if (cmd == "LOAD") {
    currentFreq = loadFromEeprom();
    setFrequency(currentFreq);
    delay(120);
    readStatus();
  } else if (cmd.startsWith("FREQ=")) {
    float f = cmd.substring(5).toFloat();
    if (f >= 76.0 && f <= 108.0) {
      currentFreq = f;
      setFrequency(currentFreq);
      delay(120);
      readStatus();
    } else {
      Serial.println("FREQ out of range 76..108");
    }
  } else if (cmd == "STATUS") {
    Serial.print("STATUS Freq="); Serial.print(currentFreq, 1);
    Serial.print(" Sig="); Serial.print(signalLevel);
    Serial.print(" Stereo="); Serial.println(isStereo ? "YES" : "NO");
  } else {
    Serial.println("Unknown command");
  }
}

// ---------- ИК ----------
void processIr() {
  if (!IrReceiver.decode()) return;

  int code = IrReceiver.decodedIRData.command;
  IrReceiver.resume();

  bool changed = false;

  switch (code) {
    case 0x47: if (currentFreq < 108.0) { currentFreq += 0.1; changed = true; } break;  // CH+
    case 0x45: if (currentFreq > 76.0)  { currentFreq -= 0.1; changed = true; } break;  // CH-
    case 0x40: if (currentFreq < 107.1) { currentFreq += 1.0; changed = true; } break;  // ⏩
    case 0x44: if (currentFreq > 76.9)  { currentFreq -= 1.0; changed = true; } break;  // ⏪
    case 0x46: nextStation(); changed = false; break;                                   // CH — автопоиск

    case 0x09:                                  // EQ — СОХРАНИТЬ в EEPROM
      saveToEeprom(currentFreq);
      break;

    case 0x43:                                  // ▶|| — ЗАГРУЗИТЬ из EEPROM
      currentFreq = loadFromEeprom();
      changed = true;
      break;

    default: return;
  }

  if (changed) {
    setFrequency(currentFreq);
    delay(120);
    readStatus();
  }
}

void setup() {
  Serial.begin(9600);
  Wire.begin();

  lcd.init();
  lcd.backlight();
  lcd.clear();

  IrReceiver.begin(IR_PIN, ENABLE_LED_FEEDBACK);

  Serial.println("Start TEA5767 + IR + EEPROM + COM...");

  currentFreq = loadFromEeprom();   // при старте — из EEPROM или 102.0
  setFrequency(currentFreq);
  delay(200);
  readStatus();
}

void loop() {
  processIr();
  handleSerialCommand();            // пока не мешает; убери вызов, если не нужно
  readStatus();

  lcd.setCursor(0, 0);
  lcd.print("Freq: ");
  lcd.print(currentFreq, 1);
  lcd.print(" MHz   ");

  lcd.setCursor(0, 1);
  lcd.print("Sig: ");
  lcd.print(signalLevel);
  lcd.print(isStereo ? " ST   " : " MO   ");

  Serial.print("Freq: "); Serial.print(currentFreq, 1);
  Serial.print(" | Sig: "); Serial.print(signalLevel);
  Serial.print(" | Stereo: "); Serial.println(isStereo ? "YES" : "NO");

  delay(300);
}