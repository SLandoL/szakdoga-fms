#include <ESP8266WiFi.h>
#include <PubSubClient.h>
#include <NeoPixelBus.h>
#include <FastLED.h>

#define NUM_STRIPS 2
#define NUM_LEDS_STRIPTANK 6
#define NUM_LEDS_STRIPLONG 59

CRGB ledTank[NUM_LEDS_STRIPTANK];
CRGB ledLong[NUM_LEDS_STRIPLONG];

int currentLevel = 6;

char *ledColor = "green";
char *lastColor = "";
bool startedLongLed = false;

const char *ssid = "FMS-WiFi";
const char *password = "I40okos%";

const char *mqtt_broker = "192.168.0.100";
const char *topic = "tankesp";
const char *receiveTopic = "tank-esp";
const char *toCarTopic = "carManagement";
const int mqtt_port = 1883;

IPAddress local_IP(192,168,0,51);
IPAddress gateway(192,168,0,2);
IPAddress subnet(255, 255, 255, 0);

WiFiClient espClient;
PubSubClient client(espClient);

// Gombok változói
unsigned long StopLeft_button_time = 0;
unsigned long StopLeft_last_button_time = 0;
int StopLeft_lastState = LOW;
int StopLeft_currentState;

unsigned long StopRight_button_time = 0;
unsigned long StopRight_last_button_time = 0;
int StopRight_lastState = LOW;
int StopRight_currentState;

unsigned long ResetPos_button_time = 0;
unsigned long ResetPos_last_button_time = 0;
int ResetPos_lastState = LOW;
int ResetPos_currentState;

int debouncetime = 250;

// Heartbeat (ONLINE üzenet) változók - Timer helyett
unsigned long lastHeartbeatTime = 0;
const unsigned long heartbeatInterval = 500; // 500ms

void setup() {
  Serial.begin(115200);
  Serial.flush();
  
  // LEDek inicializálása
  FastLED.addLeds<NEOPIXEL, D0>(ledLong, NUM_LEDS_STRIPLONG);
  FastLED.addLeds<NEOPIXEL, D7>(ledTank, NUM_LEDS_STRIPTANK); // Kibővített kódban D7 volt, meghagytam
  
  WiFi.config(local_IP, gateway, subnet);
  WiFi.begin(ssid, password);
  
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
  }
  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("Connected to WiFi");
  }
  
  // MQTT csatlakozás
  client.setServer(mqtt_broker, mqtt_port);
  client.setCallback(callback);
  
  while (!client.connected()) {
    client.connect("tankesp");
    Serial.print(" no connection");
    Serial.print(client.state());
  }
  Serial.println("Connected to the broker");
  client.subscribe(receiveTopic);
  client.publish(topic, "TANK ESP is connected to the WiFi and Broker!");
  
  FillTankAtStart(CRGB::Blue);

  // Kapcsoló tábla pinek:
  pinMode(0, INPUT_PULLUP);   // GPIO0 -> D3
  pinMode(14, INPUT_PULLUP);  // GPIO14 -> D5
  pinMode(12, INPUT_PULLUP);  // GPIO12 -> D6

  StopLeft_lastState = digitalRead(0);
  StopRight_lastState = digitalRead(12);
  ResetPos_lastState = digitalRead(14);
}

void loop() {
  // MQTT kapcsolat fenntartása
  if (!client.connected()) {
    reconnect();
  }
  client.loop();

  // --- ÚJ HELYES HEARTBEAT (Timer helyett) ---
  // Ez biztonságosan fut a loopban
  unsigned long currentMillis = millis();
  if (currentMillis - lastHeartbeatTime >= heartbeatInterval) {
    lastHeartbeatTime = currentMillis;
    client.publish(topic, "ONLINE");
  }
  // -------------------------------------------

  // Gomb kezelés - StopLeft (GPIO0)
  if (digitalRead(0) != StopLeft_lastState) {
    StopLeft_button_time = millis();
    if (StopLeft_button_time - StopLeft_last_button_time > debouncetime) {
      if (digitalRead(0) == 1) { // Pullup miatt fordított logika lehet, de hagytam az eredetit
        Serial.println("GPIO0 BEKAPCS+++++++++++++");
        client.publish("StopRight", "True");
      } else {
        Serial.println("GPIO0 KIKAPCS-------------");
        client.publish("StopRight", "False");
      }
      StopLeft_lastState = !StopLeft_lastState;
      StopLeft_last_button_time = StopLeft_button_time;
    }
  }

  // Gomb kezelés - StopRight (GPIO12)
  if (digitalRead(12) != StopRight_lastState) {
    StopRight_button_time = millis();
    if (StopRight_button_time - StopRight_last_button_time > debouncetime) {
      if (digitalRead(12) == 0) {
        Serial.println("GPIO12 BEKAPCS+++++++++++++");
        client.publish("StopLeft", "True");
      } else {
        Serial.println("GPIO12 KIKAPCS-------------");
        client.publish("StopLeft", "False");
      }
      StopRight_lastState = !StopRight_lastState;
      StopRight_last_button_time = StopRight_button_time;
    }
  }

  // Gomb kezelés - ResetPos (GPIO14)
  if (digitalRead(14) != ResetPos_lastState) {
    ResetPos_button_time = millis();
    if (ResetPos_button_time - ResetPos_last_button_time > debouncetime) {
      if (digitalRead(14) == 0) {
        Serial.println("GPIO14 BEKAPCS+++++++++++++");
        client.publish("ResetPos", "True");
      } else {
        Serial.println("GPIO14 KIKAPCS-------------");
        client.publish("ResetPos", "False");
      }
      ResetPos_lastState = !ResetPos_lastState;
      ResetPos_last_button_time = ResetPos_button_time;
    }
  }

  // LED vezérlés inputok alapján
  if (digitalRead(D1) > 0 && !startedLongLed) {
    startedLongLed = true;
    Serial.println("JAJAJAJAJAJAJAJJAA");
    FirstSectionLoad();
  }
  if (digitalRead(D2) > 0) {
    ClearSection();
    Serial.println("HOHOHOHOHHO");
    startedLongLed = false;
  }
}

void reconnect() {
  while (!client.connected()) {
    client.connect("tankesp");
    client.subscribe(receiveTopic);
    delay(100); // Kicsi várakozás, hogy ne floodoljon, ha nincs kapcsolat
  }
}

void SetRangeColorLong(int from, int to, CRGB color) {
  for (int i = from; i < to; i++) {
    ledLong[i] = color;
    FastLED.show();
    delay(50);
  }
  FastLED.show();
}

void FillTankAtStart(CRGB color) {
  for (int i = NUM_LEDS_STRIPTANK - 1; i >= 0; i--) {
    ledTank[i] = color;
    FastLED.show();
    client.publish(topic, "ONLINE");
    delay(500);
  }
}

void FillBeerTankAtStart() {
  for (int i = NUM_LEDS_STRIPTANK - 1; i >= 0; i--) {
    if (i == 5) {
      ledTank[i] = CRGB::White;
      FastLED.show();
      client.publish(topic, "ONLINE");
      delay(500);
      continue;
    }
    ledTank[i] = CRGB::White;
    ledTank[i + 1] = CRGB::Yellow;
    FastLED.show();
    client.publish(topic, "ONLINE");
    delay(500);
  }
}

CRGB strToColor(const char *c) {
  if (strcmp(c, "red") == 0) return CRGB::Red;
  if (strcmp(c, "blue") == 0) return CRGB::Blue;
  if (strcmp(c, "green") == 0) return CRGB::Green;
  if (strcmp(c, "white") == 0) return CRGB::White;
  if (strcmp(c, "black") == 0) return CRGB::Black;
  return CRGB::Blue; // Default
}

void SetRangeColorTank(int from, int to, CRGB color) {
  for (int i = from; i < to; i++) {
    ledTank[i] = color;
  }
  FastLED.show();
}

void SetLedColorTank(int pixel, CRGB color) {
  ledTank[pixel] = color;
  FastLED.show();
}

void EmptyTank(CRGB color) {
  if (currentLevel > 0) {
    for (int i = 0; i < 14; i++) {
      client.publish(topic, "ONLINE");
      SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel, color);
      delay(110);
      SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel, CRGB::Black);
      delay(110);
    }
    currentLevel--;
  }
}

void EmptyBeerTank() {
  if (currentLevel > 0) {
    if (currentLevel == 1) {
      for (int i = 0; i < 14; i++) {
        client.publish(topic, "ONLINE");
        SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel, CRGB::Yellow);
        delay(110);
        SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel, CRGB::Black);
        delay(110);
      }
    } else {
      for (int i = 0; i < 14; i++) {
        client.publish(topic, "ONLINE");
        SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel, CRGB::White);
        SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel + 1, CRGB::Yellow);
        delay(110);
        SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel, CRGB::Black);
        SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel + 1, CRGB::Black);
        delay(110);
      }
      SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel + 1, CRGB::White);
    }
    currentLevel--;
  }
}

void EmptyTankAtChange() {
  for (int i = NUM_LEDS_STRIPTANK - currentLevel; i < NUM_LEDS_STRIPTANK; i++) {
    client.publish(topic, "ONLINE");
    ledTank[i] = CRGB::Black;
    FastLED.show();
    delay(200);
  }
}

void EmptyBeerTankAtChange() {
  for (int i = NUM_LEDS_STRIPTANK - currentLevel; i < NUM_LEDS_STRIPTANK; i++) {
    if (i == NUM_LEDS_STRIPTANK - 1) {
      ledTank[i] = CRGB::Black;
      FastLED.show();
      client.publish(topic, "ONLINE");
      break;
    }
    client.publish(topic, "ONLINE");
    ledTank[i] = CRGB::Black;
    ledTank[i + 1] = CRGB::White;
    FastLED.show();
    delay(200);
  }
}

void FirstSectionLoad() {
  SetRangeColorLong(0, NUM_LEDS_STRIPLONG / 2 + 1, CRGB::Blue);
}

void SecondSectionLoad() {
  SetRangeColorLong(NUM_LEDS_STRIPLONG / 2 + 1, NUM_LEDS_STRIPLONG, CRGB::Blue);
}

void ClearSection() {
  SetRangeColorLong(0, NUM_LEDS_STRIPLONG, CRGB::Black);
}

void callback(char *receiveTopic, byte *payload, unsigned int length) {
  char msg[20] = "";
  char color[20] = "";
  unsigned int i = 0;
  
  // Üzenet típusának kiolvasása (space-ig)
  for (; i < length; i++) {
    msg[i] = (char)payload[i];
    if (msg[i] == ' ') {
      break;
    }
  }
  msg[i++] = '\0';
  Serial.print("MSG: ");
  Serial.println(msg);

  // Szín kiolvasása
  int j = 0;
  for (; i < length; i++) {
    color[j++] = (char)payload[i];
  }
  color[j] = '\0';
  Serial.print("COLOR: ");
  Serial.println(color);

  // Logika
  if (strcmp(msg, "empty") == 0) {
    if (strcmp(color, "beer") == 0) {
      EmptyBeerTank();
      client.publish(toCarTopic, "CarGOTank");
      SecondSectionLoad();
      if (currentLevel == 1) {
        delay(1000);
        SetLedColorTank(NUM_LEDS_STRIPTANK - currentLevel, CRGB::Yellow);
      }
      if (currentLevel == 0) {
        for (int c = 0; c < 5; c++) {
          client.publish(topic, "ONLINE");
          delay(1000);
        }
        FillBeerTankAtStart();
        currentLevel = 6;
      }
    } else {
      EmptyTank((CRGB)strToColor(color));
      client.publish(toCarTopic, "CarGOTank");
      SecondSectionLoad();
      if (currentLevel == 0) {
        delay(5000);
        FillTankAtStart((CRGB)strToColor(color));
        currentLevel = 6;
      }
    }
  }
  if (strcmp(msg, "start") == 0) {
    if (currentLevel > 0) {
      if (strcmp(lastColor, "beer") == 0) {
        EmptyBeerTankAtChange();
        currentLevel = 0;
      } else {
        EmptyTankAtChange();
        currentLevel = 0;
      }
    }
    if (strcmp(color, "beer") == 0) {
      FillBeerTankAtStart();
      currentLevel = 6;
      lastColor = "beer";
    } else {
      FillTankAtStart((CRGB)strToColor(color));
      currentLevel = 6; 
    }
  }
  if (strcmp(color, "beer") != 0) {
    lastColor = "else";
  }
}