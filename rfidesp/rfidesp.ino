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
bool elsore = 1;  //olvaso mukodesenek 1x publikalasara

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
/* Create another array to read data from Block */
/* Legthn of buffer should be 2 Bytes more than the size of Block (16 Bytes) */
byte bufferLen = 18;
byte readBlockData_TANK[18];
byte readBlockData_WH[18];
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
  mfrc522_WH.PCD_SetAntennaGain(mfrc522_WH.RxGain_max);
  mfrc522_TANK.PCD_SetAntennaGain(mfrc522_TANK.RxGain_max);
  /* Prepare the ksy for authentication */
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
    if (client.connect("ESP8266Client")) {
      Serial.println("connected");
      // Subscribe
      //client.subscribe("esp32/output");
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" try again in 1 second");
      // Wait 5 seconds before retrying
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

  if (elsore == 1) {
    elsore = 0;
    client.publish("Tank_olvaso_mukodik", "True");
    client.publish("WH_olvaso_mukodik", "True");
  }


  errResponse = mfrc522_TANK.PCD_ReadRegister(MFRC522::ComIrqReg);
  if (errResponse != 69 && errResponse != 21 && errResponse != 100 && errResponse != 101) {  //Tank olvaso nincs tap 
    client.publish("Tank_olvaso_mukodik", "False");
    do {
      Serial.print("Tank olvaso szar, code: ");
      Serial.println(errResponse);
      delay(500);
      mfrc522_TANK.PCD_Init();
      errResponse = mfrc522_TANK.PCD_ReadRegister(MFRC522::ComIrqReg);

    } while (errResponse != 20); 
    Serial.print("Tank olvaso megjavult! code: ");
    Serial.println(errResponse);
    client.publish("Tank_olvaso_mukodik", "True");
  }


  errResponse = mfrc522_WH.PCD_ReadRegister(MFRC522::ComIrqReg);
  if (errResponse != 69 && errResponse != 21 && errResponse != 100 && errResponse != 101) {  //WH olvaso nincs tap
    client.publish("WH_olvaso_mukodik", "False");
    do {
      Serial.print("WH olvaso szar, error: ");
      Serial.println(errResponse);
      delay(500);
      mfrc522_WH.PCD_Init();

      errResponse = mfrc522_WH.PCD_ReadRegister(MFRC522::ComIrqReg);
    } while (errResponse != 20);
    Serial.print("WH olvaso megjavult! code: ");
    Serial.println(errResponse);
    client.publish("WH_olvaso_mukodik", "True");
  }

  //Tank olvaso loopja
  /* Look for new cards */
  /* Reset the loop if no new card is present on RC522 Reader */
  if (mfrc522_TANK.PICC_IsNewCardPresent()) {
    /* Select one of the cards */
    if (mfrc522_TANK.PICC_ReadCardSerial()) {


      Serial.print("\n");
      Serial.println("**TANK Card Detected**");

      //Call 'WriteDataToBlock_WH' function, which will write data to the block
      /*
      Serial.print("\n");
      Serial.println("Writing to Data Block...");
      WriteDataToBlock_WH(4, blockData); 
      */

      ReadDataFromBlock_TANK(blockNum, readBlockData_TANK);
      //if (blockNum == 4) {
      for (int j = 0; j < 16; j++) {
        if (readBlockData_TANK[j] != 0) {
          Serial.write(readBlockData_TANK[j]);
        }
      }

      client.publish("TANK_rakomany", reinterpret_cast<const char*>(readBlockData_TANK));
      Serial.println(" published in TANK_rakomany");

      mfrc522_TANK.PICC_HaltA();       // Stop the communication with the card
      //mfrc522_TANK.PCD_StopCrypto1();  // Stop encryption
      delay(100);                      // Small delay to prevent overwhelming the reader
      newRead_TANK = 1;
    }
  }

  //Warehouse olvaso loopja
  /* Look for new cards */
  /* Reset the loop if no new card is present on RC522 Reader */
  if (mfrc522_WH.PICC_IsNewCardPresent()) {
    /* Select one of the cards */
    if (mfrc522_WH.PICC_ReadCardSerial()) {

      Serial.print("\n");
      Serial.println("**WH Card Detected**");

      /* Call 'WriteDataToBlock_WH' function, which will write data to the block */
      /*
      Serial.print("\n");
      Serial.println("Writing to Data Block...");
      WriteDataToBlock_WH(4, blockData); 
      */

      ReadDataFromBlock_WH(blockNum, readBlockData_WH);
      //if (blockNum == 4) {
      for (int j = 0; j < 16; j++) {
        if (readBlockData_WH[j] != 0) {
          Serial.write(readBlockData_WH[j]);
        }
      }
      client.publish("WH_rakomany", reinterpret_cast<const char*>(readBlockData_WH));
      Serial.println(" published in WH_rakomany");

      mfrc522_WH.PICC_HaltA();       // Stop the communication with the card
      mfrc522_WH.PCD_StopCrypto1();  // Stop encryption
      delay(100);                    // Small delay to prevent overwhelming the reader
      if (newRead_TANK) {
        newRead_WH = 1;
      } else {
        newRead_TANK = 0;
      }
    }
  }

  if (newRead_TANK && newRead_WH) {
    newRead_TANK = 0;
    newRead_WH = 0;
    if (memcmp(readBlockData_TANK, readBlockData_WH, 18) == 0) {
      Serial.println("Ugyan az a ket rakomany");
      client.publish("Rakomany_egyezes", "True");
    } else {
      Serial.println("NEM ugyan az a ket rakomany");
      client.publish("Rakomany_egyezes", "False");
    }
  }
}

void ReadDataFromBlock_TANK(int pageNum, byte readBlockData[]) {
  // Ultralight kártyáknál NINCS hitelesítés (Authenticate)!
  // Ezt a lépést teljesen kihagyjuk.

  byte buffer[18];
  byte size = sizeof(buffer);

  // Az olvasás a 'pageNum'-tól indul és 4 lapot olvas ki (16 byte)
  status = mfrc522_TANK.MIFARE_Read(pageNum, buffer, &size);

  if (status != MFRC522::STATUS_OK) {
    Serial.print("Olvasás sikertelen: ");
    Serial.println(mfrc522_TANK.GetStatusCodeName(status));
    return;
  }
  
  // Átmásoljuk az adatot a kimeneti tömbbe
  for (byte i = 0; i < 16; i++) {
    readBlockData[i] = buffer[i];
  }
  Serial.println("Adat sikeresen beolvasva (Ultralight)!");
}

// Ugyanez a WH olvasóhoz is kell:
void ReadDataFromBlock_WH(int pageNum, byte readBlockData[]) {
  byte buffer[18];
  byte size = sizeof(buffer);

  status = mfrc522_WH.MIFARE_Read(pageNum, buffer, &size);

  if (status != MFRC522::STATUS_OK) {
    Serial.print("Olvasás sikertelen: ");
    Serial.println(mfrc522_WH.GetStatusCodeName(status));
    return;
  }
  
  for (byte i = 0; i < 16; i++) {
    readBlockData[i] = buffer[i];
  }
  Serial.println("Adat sikeresen beolvasva (Ultralight)!");
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