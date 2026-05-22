#include <WiFi.h>
#include <PubSubClient.h>

// Configure these values locally before flashing.
const char* ssid = "YOUR_WIFI_SSID";
const char* password = "YOUR_WIFI_PASSWORD";
const char* mqtt_server = "192.168.0.100";

WiFiClient espClient;
PubSubClient client(espClient);

const unsigned long debounceMs = 50;
const unsigned long statusIntervalMs = 5000;
unsigned long lastStatusPublishMs = 0;

struct SwitchInput {
  const char* switchId;
  const char* topic;
  const char* eventName;
  uint8_t pin;
  bool activeLow;
  bool state;
  bool lastRawState;
  bool hasState;
  unsigned long lastRawChangeMs;
};

SwitchInput switches[] = {
  // state=true means the power/function is enabled. state=false means interrupted/disabled.
  {"TankPower", "PhysicalSwitch/TankPower", "", 25, true, true, true, false, 0},
  {"RfidTankPower", "PhysicalSwitch/RfidTankPower", "", 26, true, true, true, false, 0},
  {"RfidWarehousePower", "PhysicalSwitch/RfidWarehousePower", "", 27, true, true, true, false, 0},

  // enabled=true means the AGV should use the corresponding optional stop.
  {"ExtraStop1", "Agv/Switch/ExtraStop1", "", 32, true, false, false, false, 0},
  {"ExtraStop2", "Agv/Switch/ExtraStop2", "", 33, true, false, false, false, 0},

  // Debug is edge-triggered: every state change publishes ForceNextStopFactory exactly once.
  {"Debug", "Agv/Switch/Debug", "ForceNextStopFactory", 34, true, false, false, false, 0}
};

const size_t switchCount = sizeof(switches) / sizeof(switches[0]);

void setup() {
  Serial.begin(115200);

  for (size_t i = 0; i < switchCount; i++) {
    pinMode(switches[i].pin, INPUT_PULLUP);
    bool rawState = readLogicalState(switches[i]);
    switches[i].state = rawState;
    switches[i].lastRawState = rawState;
    switches[i].hasState = true;
  }

  setupWifi();
  client.setServer(mqtt_server, 1883);
}

void loop() {
  if (!client.connected()) {
    reconnect();
  }

  client.loop();
  handleSwitches();
  publishPeriodicStatus();
}

void setupWifi() {
  delay(10);
  WiFi.begin(ssid, password);

  int retryCount = 0;
  while (WiFi.status() != WL_CONNECTED) {
    delay(250);
    retryCount++;
    if (retryCount > 80) {
      ESP.restart();
    }
  }
}

void reconnect() {
  int retryCount = 0;
  while (!client.connected()) {
    if (client.connect("physical-switch-esp")) {
      publishAllSwitchStates(false);
    } else {
      delay(1000);
      retryCount++;
      if (retryCount > 5) {
        ESP.restart();
      }
    }
  }
}

void handleSwitches() {
  unsigned long now = millis();

  for (size_t i = 0; i < switchCount; i++) {
    SwitchInput& input = switches[i];
    bool rawState = readLogicalState(input);

    if (rawState != input.lastRawState) {
      input.lastRawState = rawState;
      input.lastRawChangeMs = now;
    }

    if (rawState != input.state && now - input.lastRawChangeMs >= debounceMs) {
      input.state = rawState;
      input.hasState = true;
      publishSwitchState(input, true);
    }
  }
}

void publishPeriodicStatus() {
  unsigned long now = millis();
  if (lastStatusPublishMs == 0 || now - lastStatusPublishMs >= statusIntervalMs) {
    lastStatusPublishMs = now;
    publishAllSwitchStates(false);
  }
}

void publishAllSwitchStates(bool changed) {
  for (size_t i = 0; i < switchCount; i++) {
    publishSwitchState(switches[i], changed);
  }
}

bool readLogicalState(const SwitchInput& input) {
  bool pinHigh = digitalRead(input.pin) == HIGH;
  return input.activeLow ? !pinHigh : pinHigh;
}

void publishSwitchState(const SwitchInput& input, bool changed) {
  char buffer[192];

  if (strlen(input.eventName) > 0) {
    snprintf(
      buffer,
      sizeof(buffer),
      "{\"switchId\":\"%s\",\"state\":%s,\"changed\":%s,\"event\":\"%s\",\"timestampMs\":%lu}",
      input.switchId,
      input.state ? "true" : "false",
      changed ? "true" : "false",
      input.eventName,
      millis()
    );
  } else if (strncmp(input.topic, "Agv/Switch/ExtraStop", 20) == 0) {
    snprintf(
      buffer,
      sizeof(buffer),
      "{\"switchId\":\"%s\",\"enabled\":%s,\"changed\":%s,\"timestampMs\":%lu}",
      input.switchId,
      input.state ? "true" : "false",
      changed ? "true" : "false",
      millis()
    );
  } else {
    snprintf(
      buffer,
      sizeof(buffer),
      "{\"switchId\":\"%s\",\"state\":%s,\"changed\":%s,\"timestampMs\":%lu}",
      input.switchId,
      input.state ? "true" : "false",
      changed ? "true" : "false",
      millis()
    );
  }

  client.publish(input.topic, buffer);
  Serial.print("Published switch state on ");
  Serial.print(input.topic);
  Serial.print(": ");
  Serial.println(buffer);
}
