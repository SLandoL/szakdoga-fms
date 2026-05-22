#include <WiFi.h>
#include <PubSubClient.h>
#include <SPI.h>
#include <MFRC522.h>

// Replace the next variables with your SSID/Password combination
const char* ssid = "FMS-WiFi";
const char* password = "I40okos%";

// Add your MQTT Broker IP address, example:
const char* mqtt_server = "192.168.0.100";

WiFiClient espClient;
PubSubClient client(espClient);
long lastMsg = 0;
char msg[50];
int value = 0;
int errResponse = 0;

const unsigned long heartbeatIntervalMs = 2000;
const unsigned long readerStatusIntervalMs = 2000;
unsigned long lastHeartbeat = 0;
unsigned long lastReaderStatusPublish = 0;

bool tankReaderOk = false;
bool warehouseReaderOk = false;
bool tankReaderStatusKnown = false;
bool warehouseReaderStatusKnown = false;
bool lastPublishedTankReaderOk = false;
bool lastPublishedWarehouseReaderOk = false;
int tankReaderErrorCode = 0;
int warehouseReaderErrorCode = 0;

const char* cargoDisplayMarker = "XXX";
const byte rfidReadChunkBytes = 16;
const byte rfidReadStepPages = 4;
const byte maxCargoReadChunks = 3;
const size_t cargoRawBufferSize = (rfidReadChunkBytes * maxCargoReadChunks) + 1;

/*Using Hardware SPI of Arduino */
/*MOSI (11), MISO (12) and SCK (13) are fixed */
/*You can configure SS and RST Pins*/
#define SS_PIN_TANK 17
#define SS_PIN_WH 5 /* Slave Select Pin */
#define RST_PIN 0   /* Reset Pin */

/* Create an instance of MFRC522 */
MFRC522 mfrc522_WH(SS_PIN_WH, RST_PIN);
MFRC522 mfrc522_TANK(SS_PIN_TANK, RST_PIN);
/* Create an instance of MIFARE_Key */
MFRC522::MIFARE_Key key;

/* Create an array of 16 Bytes and fill it with data */
/* This is the actual data which is going to be written into the card */
byte blockData[16] = { "Xanax" };
int blockNum = 4;
/* Raw RFID payload buffer. 48 bytes are read to support XXX + 8 UTF-8 chars + XXX. */
byte readBlockData_TANK[cargoRawBufferSize];
byte readBlockData_WH[cargoRawBufferSize];
bool newRead_TANK = 0;
bool newRead_WH = 0;

MFRC522::StatusCode status;

void setup() {
  pinMode(2, OUTPUT);
  /* Initialize serial communications with the PC */
  Serial.begin(115200);
  /* Initialize SPI bus */
  SPI.begin();
  /* Initialize MFRC522 Module */
  mfrc522_WH.PCD_Init();
  mfrc522_TANK.PCD_Init();
  mfrc522_WH.PCD_SetAntennaGain(mfrc522_WH.RxGain_max);
  mfrc522_TANK.PCD_SetAntennaGain(mfrc522_TANK.RxGain_max);
  /* Prepare the key for authentication */
  /* All keys are set to FFFFFFFFFFFFh at chip delivery from the factory */
  for (byte i = 0; i < 6; i++) {
    key.keyByte[i] = 0x00;
  }

  setup_wifi();
  client.setServer(mqtt_server, 1883);
  client.setCallback(callback);
}

void reconnect() {
  int retryCount = 0;
  // Loop until we're reconnected
  while (!client.connected()) {
    Serial.print("Attempting MQTT connection...");
    // Attempt to connect
    if (client.connect("RFID-ESP")) {
      Serial.println("connected");
      // Subscribe
      //client.subscribe("esp32/output");
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" try again in 1 second");
      delay(1000);
      retryCount++;
      if (retryCount > 5) {
        Serial.println("\nNem sikerult csatlakozni MQTT-hez, reboot...");
        ESP.restart();
      }
    }
  }
}

void callback(char* topic, byte* message, unsigned int length) {
  Serial.print("Message arrived on topic: ");
  Serial.print(topic);
  Serial.print(". Message: ");
  String messageTemp;

  for (int i = 0; i < length; i++) {
    Serial.print((char)message[i]);
    messageTemp += (char)message[i];
  }
  Serial.println();
}

void setup_wifi() {
  delay(10);
  // We start by connecting to a WiFi network
  Serial.println();
  Serial.print("Connecting to ");
  Serial.println(ssid);

  WiFi.begin(ssid, password);

  int retryCount = 0;
  while (WiFi.status() != WL_CONNECTED) {
    digitalWrite(2, HIGH);  // Turn the LED on
    delay(100);
    Serial.print(".");
    digitalWrite(2, LOW);  // Turn the LED off
    delay(100);
    retryCount++;
    if (retryCount > 50) {
      Serial.println("\nNem sikerult csatlakozni wifihez, reboot...");
      ESP.restart();
    }
  }

  Serial.println("");
  Serial.println("WiFi connected");
  Serial.println("IP address: ");
  Serial.println(WiFi.localIP());
}

void loop() {
  if (!client.connected()) {
    reconnect();
  }

  client.loop();
  updateReaderHealth();
  publishPeriodicDiagnostics();
  handleTankReader();
  handleWarehouseReader();
  publishCargoMatchIfReady();
}

void publishPeriodicDiagnostics() {
  unsigned long now = millis();

  if (lastHeartbeat == 0 || now - lastHeartbeat >= heartbeatIntervalMs) {
    lastHeartbeat = now;
    publishHeartbeat();
  }

  if (lastReaderStatusPublish == 0 || now - lastReaderStatusPublish >= readerStatusIntervalMs) {
    lastReaderStatusPublish = now;
    publishReaderStatus("RFID/TankReader/Status", "tank", tankReaderOk, tankReaderErrorCode);
    publishReaderStatus("RFID/WarehouseReader/Status", "warehouse", warehouseReaderOk, warehouseReaderErrorCode);
  }
}

void updateReaderHealth() {
  tankReaderErrorCode = mfrc522_TANK.PCD_ReadRegister(MFRC522::ComIrqReg);
  warehouseReaderErrorCode = mfrc522_WH.PCD_ReadRegister(MFRC522::ComIrqReg);

  bool currentTankReaderOk = isReaderRegisterOk(tankReaderErrorCode);
  bool currentWarehouseReaderOk = isReaderRegisterOk(warehouseReaderErrorCode);

  if (!currentTankReaderOk) {
    Serial.print("Tank olvaso hiba, code: ");
    Serial.println(tankReaderErrorCode);
    mfrc522_TANK.PCD_Init();
  }

  if (!currentWarehouseReaderOk) {
    Serial.print("WH olvaso hiba, code: ");
    Serial.println(warehouseReaderErrorCode);
    mfrc522_WH.PCD_Init();
  }

  tankReaderOk = currentTankReaderOk;
  warehouseReaderOk = currentWarehouseReaderOk;

  publishLegacyReaderStatusIfChanged("Tank_olvaso_mukodik", tankReaderOk, lastPublishedTankReaderOk, tankReaderStatusKnown);
  publishLegacyReaderStatusIfChanged("WH_olvaso_mukodik", warehouseReaderOk, lastPublishedWarehouseReaderOk, warehouseReaderStatusKnown);
}

bool isReaderRegisterOk(int errorCode) {
  return errorCode == 20 || errorCode == 21 || errorCode == 69 || errorCode == 100 || errorCode == 101;
}

void publishLegacyReaderStatusIfChanged(const char* topic, bool currentValue, bool& lastPublishedValue, bool& known) {
  if (!known || currentValue != lastPublishedValue) {
    publishBoolTopic(topic, currentValue);
    lastPublishedValue = currentValue;
    known = true;
  }
}

void publishBoolTopic(const char* topic, bool value) {
  client.publish(topic, value ? "True" : "False");
}

void publishHeartbeat() {
  char buffer[256];
  snprintf(
    buffer,
    sizeof(buffer),
    "{\"deviceId\":\"rfid-esp\",\"uptimeMs\":%lu,\"wifiConnected\":%s,\"mqttConnected\":%s,\"tankReaderOk\":%s,\"warehouseReaderOk\":%s}",
    millis(),
    WiFi.status() == WL_CONNECTED ? "true" : "false",
    client.connected() ? "true" : "false",
    tankReaderOk ? "true" : "false",
    warehouseReaderOk ? "true" : "false"
  );
  client.publish("RFID/Heartbeat", buffer);
}

void publishReaderStatus(const char* topic, const char* reader, bool ok, int errorCode) {
  char buffer[160];
  snprintf(
    buffer,
    sizeof(buffer),
    "{\"reader\":\"%s\",\"ok\":%s,\"lastCheckMs\":%lu,\"errorCode\":%d}",
    reader,
    ok ? "true" : "false",
    millis(),
    errorCode
  );
  client.publish(topic, buffer);
}

void publishCargo(const char* legacyTopic, const char* structuredTopic, const char* reader, byte readBlockData[], bool readOk) {
  char cargoId[cargoRawBufferSize];
  copyCargoId(readBlockData, cargoId, sizeof(cargoId));

  if (readOk) {
    client.publish(legacyTopic, cargoId);
    Serial.print("Clean cargo published in ");
    Serial.print(legacyTopic);
    Serial.print(": ");
    Serial.println(cargoId);
  }

  char buffer[256];
  snprintf(
    buffer,
    sizeof(buffer),
    "{\"reader\":\"%s\",\"cargoId\":\"%s\",\"readOk\":%s,\"timestampMs\":%lu}",
    reader,
    cargoId,
    readOk ? "true" : "false",
    millis()
  );
  client.publish(structuredTopic, buffer);
}

void copyCargoId(byte source[], char target[], size_t targetSize) {
  char rawValue[cargoRawBufferSize];
  size_t rawIndex = 0;

  for (size_t j = 0; j < cargoRawBufferSize - 1 && rawIndex < sizeof(rawValue) - 1; j++) {
    byte currentByte = source[j];

    // Ignore zeros and ASCII control bytes. Keep bytes >= 128 so UTF-8 characters such as ö are not removed.
    if (currentByte == 0 || currentByte < 32 || currentByte == 127) {
      continue;
    }

    // Do not let card data break the JSON payload.
    if (currentByte == '"' || currentByte == '\\') {
      continue;
    }

    rawValue[rawIndex++] = (char)currentByte;
  }
  rawValue[rawIndex] = '\0';

  extractMarkedCargoValue(rawValue, target, targetSize);
}

void extractMarkedCargoValue(const char* rawValue, char* target, size_t targetSize) {
  const char* startMarker = strstr(rawValue, cargoDisplayMarker);
  const char* contentStart = startMarker == NULL ? rawValue : startMarker + strlen(cargoDisplayMarker);
  const char* endMarker = strstr(contentStart, cargoDisplayMarker);

  size_t copyLength = endMarker == NULL ? strlen(contentStart) : (size_t)(endMarker - contentStart);
  if (copyLength >= targetSize) {
    copyLength = targetSize - 1;
  }

  strncpy(target, contentStart, copyLength);
  target[copyLength] = '\0';
  trimCargoString(target);
}

void trimCargoString(char* value) {
  size_t length = strlen(value);
  while (length > 0 && isCargoWhitespace(value[length - 1])) {
    value[length - 1] = '\0';
    length--;
  }

  size_t startIndex = 0;
  while (value[startIndex] != '\0' && isCargoWhitespace(value[startIndex])) {
    startIndex++;
  }

  if (startIndex > 0) {
    memmove(value, value + startIndex, strlen(value + startIndex) + 1);
  }
}

bool isCargoWhitespace(char value) {
  return value == ' ' || value == '\t' || value == '\r' || value == '\n';
}

bool hasClosingCargoMarker(byte source[]) {
  char cargoId[cargoRawBufferSize];
  copyCargoId(source, cargoId, sizeof(cargoId));

  char rawValue[cargoRawBufferSize];
  size_t rawIndex = 0;
  for (size_t j = 0; j < cargoRawBufferSize - 1 && rawIndex < sizeof(rawValue) - 1; j++) {
    byte currentByte = source[j];
    if (currentByte == 0 || currentByte < 32 || currentByte == 127 || currentByte == '"' || currentByte == '\\') {
      continue;
    }
    rawValue[rawIndex++] = (char)currentByte;
  }
  rawValue[rawIndex] = '\0';

  const char* startMarker = strstr(rawValue, cargoDisplayMarker);
  if (startMarker == NULL) {
    return false;
  }

  const char* contentStart = startMarker + strlen(cargoDisplayMarker);
  return strstr(contentStart, cargoDisplayMarker) != NULL;
}

void handleTankReader() {
  //Tank olvaso loopja
  /* Look for new cards */
  /* Reset the loop if no new card is present on RC522 Reader */
  if (mfrc522_TANK.PICC_IsNewCardPresent()) {
    /* Select one of the cards */
    if (mfrc522_TANK.PICC_ReadCardSerial()) {
      Serial.print("\n");
      Serial.println("**TANK Card Detected**");

      memset(readBlockData_TANK, 0, sizeof(readBlockData_TANK));
      bool readOk = ReadDataFromBlock_TANK(blockNum, readBlockData_TANK);

      if (readOk) {
        char cargoId[cargoRawBufferSize];
        copyCargoId(readBlockData_TANK, cargoId, sizeof(cargoId));
        Serial.print("TANK cargo: ");
        Serial.println(cargoId);
      }

      publishCargo("TANK_rakomany", "RFID/TankReader/Cargo", "tank", readBlockData_TANK, readOk);

      mfrc522_TANK.PICC_HaltA();       // Stop the communication with the card
      //mfrc522_TANK.PCD_StopCrypto1();  // Stop encryption
      delay(100);                      // Small delay to prevent overwhelming the reader
      newRead_TANK = readOk;
    }
  }
}

void handleWarehouseReader() {
  //Warehouse olvaso loopja
  /* Look for new cards */
  /* Reset the loop if no new card is present on RC522 Reader */
  if (mfrc522_WH.PICC_IsNewCardPresent()) {
    /* Select one of the cards */
    if (mfrc522_WH.PICC_ReadCardSerial()) {
      Serial.print("\n");
      Serial.println("**WH Card Detected**");

      memset(readBlockData_WH, 0, sizeof(readBlockData_WH));
      bool readOk = ReadDataFromBlock_WH(blockNum, readBlockData_WH);

      if (readOk) {
        char cargoId[cargoRawBufferSize];
        copyCargoId(readBlockData_WH, cargoId, sizeof(cargoId));
        Serial.print("WH cargo: ");
        Serial.println(cargoId);
      }

      publishCargo("WH_rakomany", "RFID/WarehouseReader/Cargo", "warehouse", readBlockData_WH, readOk);

      mfrc522_WH.PICC_HaltA();       // Stop the communication with the card
      mfrc522_WH.PCD_StopCrypto1();  // Stop encryption
      delay(100);                    // Small delay to prevent overwhelming the reader
      if (newRead_TANK && readOk) {
        newRead_WH = 1;
      } else if (!readOk) {
        newRead_WH = 0;
      }
    }
  }
}

void publishCargoMatchIfReady() {
  if (newRead_TANK && newRead_WH) {
    newRead_TANK = 0;
    newRead_WH = 0;

    char tankCargoId[cargoRawBufferSize];
    char warehouseCargoId[cargoRawBufferSize];
    copyCargoId(readBlockData_TANK, tankCargoId, sizeof(tankCargoId));
    copyCargoId(readBlockData_WH, warehouseCargoId, sizeof(warehouseCargoId));

    bool cargoMatch = strlen(tankCargoId) > 0 && strcmp(tankCargoId, warehouseCargoId) == 0;

    if (cargoMatch) {
      Serial.println("Ugyan az a ket rakomany");
    } else {
      Serial.println("NEM ugyan az a ket rakomany");
    }

    publishBoolTopic("Rakomany_egyezes", cargoMatch);
    publishStructuredCargoMatch(cargoMatch);
  }
}

void publishStructuredCargoMatch(bool cargoMatch) {
  char tankCargoId[cargoRawBufferSize];
  char warehouseCargoId[cargoRawBufferSize];
  copyCargoId(readBlockData_TANK, tankCargoId, sizeof(tankCargoId));
  copyCargoId(readBlockData_WH, warehouseCargoId, sizeof(warehouseCargoId));

  char buffer[256];
  snprintf(
    buffer,
    sizeof(buffer),
    "{\"match\":%s,\"tankCargoId\":\"%s\",\"warehouseCargoId\":\"%s\",\"timestampMs\":%lu}",
    cargoMatch ? "true" : "false",
    tankCargoId,
    warehouseCargoId,
    millis()
  );
  client.publish("RFID/CargoMatch", buffer);
}

bool ReadDataFromBlock_TANK(int pageNum, byte readBlockData[]) {
  return ReadCargoData(mfrc522_TANK, pageNum, readBlockData, "TANK");
}

bool ReadDataFromBlock_WH(int pageNum, byte readBlockData[]) {
  return ReadCargoData(mfrc522_WH, pageNum, readBlockData, "WH");
}

bool ReadCargoData(MFRC522& reader, int pageNum, byte readBlockData[], const char* readerName) {
  memset(readBlockData, 0, cargoRawBufferSize);

  size_t copiedBytes = 0;
  for (byte chunk = 0; chunk < maxCargoReadChunks && copiedBytes < cargoRawBufferSize - 1; chunk++) {
    byte buffer[18] = { 0 };
    byte size = sizeof(buffer);
    int currentPage = pageNum + (chunk * rfidReadStepPages);

    status = reader.MIFARE_Read(currentPage, buffer, &size);

    if (status != MFRC522::STATUS_OK) {
      Serial.print(readerName);
      Serial.print(" olvasas sikertelen a lapon: ");
      Serial.print(currentPage);
      Serial.print(" Hiba: ");
      Serial.println(reader.GetStatusCodeName(status));
      return copiedBytes > 0;
    }

    for (byte i = 0; i < rfidReadChunkBytes && copiedBytes < cargoRawBufferSize - 1; i++) {
      readBlockData[copiedBytes++] = buffer[i];
    }
    readBlockData[copiedBytes] = 0;

    if (hasClosingCargoMarker(readBlockData)) {
      break;
    }
  }

  Serial.print(readerName);
  Serial.println(" adat sikeresen beolvasva (extended UTF-8 payload)!");
  return copiedBytes > 0;
}

void WriteDataToBlock_TANK(int startPage, byte blockData[]) {
  // Ultralight kártyánál 4 byte-ot írunk egyszerre.
  // A 16 byte adatot 4 részletben írjuk fel egymás utáni lapokra.

  for (int i = 0; i < 4; i++) {
    byte dataPage[4];
    // Kimásolunk 4 byte-ot a nagy tömbből
    for (int j = 0; j < 4; j++) {
      dataPage[j] = blockData[(i * 4) + j];
    }

    // Írás az aktuális lapra (startPage + i)
    // FIGYELEM: Itt MIFARE_Ultralight_Write parancsot használunk!
    status = mfrc522_TANK.MIFARE_Ultralight_Write(startPage + i, dataPage, 4);

    if (status != MFRC522::STATUS_OK) {
      Serial.print("Iras hiba a lapon: ");
      Serial.print(startPage + i);
      Serial.print(" Hiba: ");
      Serial.println(mfrc522_TANK.GetStatusCodeName(status));
      return;
    }
  }
  Serial.println("Sikeres iras (Ultralight)!");
}

// Ugyanez a WH olvasóhoz:
void WriteDataToBlock_WH(int startPage, byte blockData[]) {
  for (int i = 0; i < 4; i++) {
    byte dataPage[4];
    for (int j = 0; j < 4; j++) {
      dataPage[j] = blockData[(i * 4) + j];
    }

    status = mfrc522_WH.MIFARE_Ultralight_Write(startPage + i, dataPage, 4);

    if (status != MFRC522::STATUS_OK) {
      Serial.print("Iras hiba a lapon: ");
      Serial.print(startPage + i);
      Serial.print(" Hiba: ");
      Serial.println(mfrc522_WH.GetStatusCodeName(status));
      return;
    }
  }
  Serial.println("Sikeres iras (Ultralight)!");
}
