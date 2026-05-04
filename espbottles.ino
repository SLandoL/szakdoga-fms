#include <ESP8266WiFi.h>
#include <PubSubClient.h>
#include <NeoPixelBus.h>

const uint16_t PixelCount = 6; // this example assumes 4 pixels, making it smaller will cause a failure
const uint8_t PixelPin = 2;  // make sure to set this to the correct pin, ignored for Esp8266

#define colorSaturation 255

NeoPixelBus<NeoGrbFeature, NeoEsp8266Dma800KbpsMethod> strip(PixelCount, PixelPin);

RgbColor red(colorSaturation, 0, 0);
RgbColor green(0, colorSaturation, 0);
RgbColor blue(0, 0, colorSaturation);
RgbColor white(colorSaturation);
RgbColor black(0);
RgbColor yellow(colorSaturation, colorSaturation, 0);

void SetRangeColor(int from, int to, RgbColor color){
  for(int i = from; i < to; i++){
    strip.SetPixelColor(i, color);  
  }
  strip.Show();
}

char *lastColor = "";

const char *ssid = "FMS-WiFi"; 
const char *password = "I40okos%";

const char *mqtt_broker = "192.168.0.100";
const char *senderTopic = "caresp";
const char *receiveTopic = "car-esp";
const char *toCarTopic = "carManagement";
const int mqtt_port = 1883;

IPAddress local_IP(192,168,0,51);
IPAddress gateway(192,168,0,2);
IPAddress subnet(255, 255, 255, 0);

WiFiClient espClient;
PubSubClient client(espClient);

RgbColor strToColor(const char* c){
  RgbColor color;
  if(strcmp(c, "red") == 0)
    return red;
  if(strcmp(c, "blue") == 0)
    return blue;
  if(strcmp(c, "green") == 0)
    return green;
  if(strcmp(c, "white") == 0)
    return white;
  if(strcmp(c, "black") == 0)
    return black;
  return blue;
}

void SetPixelColor(int no, RgbColor color) {
    strip.SetPixelColor(no, color);
    strip.Show();
}

void FillBottles(RgbColor color){  
  for(int cnt = 0 ;cnt < PixelCount; cnt++){
    SetPixelColor(cnt, color);
    client.publish(senderTopic, "ONLINE");
    delay(500);
  }
}

void FillBeerBottles(){  
  for(int cnt = 0 ;cnt < PixelCount; cnt++){
    if(cnt == 0){
      SetPixelColor(cnt, white);
      client.publish(senderTopic, "ONLINE");
      delay(500);
      continue;
    }
    strip.SetPixelColor(cnt - 1, yellow);
    strip.SetPixelColor(cnt, white);
    strip.Show();
    client.publish(senderTopic, "ONLINE");
    delay(500);
  }
}

void EmptyBottles(){
  for(int cnt = PixelCount - 1; cnt >= 0; cnt--){
    SetPixelColor(cnt, black);
    client.publish(senderTopic, "ONLINE");
    delay(500);
  }
  SetRangeColor(0, 6, black);
}

void EmptyBeerBottles(){
  for(int cnt = PixelCount - 1; cnt >= 0; cnt--){
    if(cnt == 0){
      strip.SetPixelColor(cnt, black);
      strip.Show();
      break;
    }
    strip.SetPixelColor(cnt, black);
    strip.SetPixelColor(cnt - 1, white);
    strip.Show();
    client.publish(senderTopic, "ONLINE");
    delay(500);
  }
  SetRangeColor(0, 6, black);
}

void setup() {
 // Set software serial baud to 115200;
 Serial.begin(115200);
 Serial.flush();
  pinMode(16, OUTPUT);
  pinMode(5, OUTPUT);
    // this resets all the neopixels to an off state
 strip.Begin();
 strip.Show();
 // connecting to a WiFi network
  pinMode(LED_BUILTIN, OUTPUT);
 digitalWrite(LED_BUILTIN, HIGH);
 if (!WiFi.config(local_IP, gateway, subnet)) {
    Serial.println("STA Failed to configure");
 }

 WiFi.begin(ssid, password);

 while (WiFi.status() != WL_CONNECTED) {
     delay(500);
     Serial.println("Connecting to WiFi..");
 }
 
 Serial.println("Connected to the WiFi network");
 //connecting to a mqtt broker
 client.setServer(mqtt_broker, mqtt_port);
 client.setCallback(callback);
 while (!client.connected()) {
     Serial.println("Connecting to public emqx mqtt broker.....");
     if (client.connect("espbottles")) {
         Serial.println("Public emqx mqtt broker connected");
     } else {
         Serial.print("failed with state ");
         Serial.print(client.state());
     }
 }
 
 // publish and subscribe
 client.publish(senderTopic, "Bottles ESP is connected!");
 client.subscribe(receiveTopic);
 SetRangeColor(0, 6, green);
 delay(2000);
 SetRangeColor(0, 6, black);
}

void heartbeat(){
  client.publish(senderTopic, "ONLINE");
  delay(500);
}

void reconnect(){
  while (!client.connected()) {
    if (client.connect("espbottles")) {
           Serial.println("Public emqx mqtt broker connected");
           client.subscribe(receiveTopic);
    }
    Serial.println("Nem reconnect");
  }
}

void callback(char *receiveTopic, byte *payload, unsigned int length) {
 char msg[20] = "";
 char color[20] = "";
 int i = 0;
 for (; i < length; i++) {
     msg[i] = (char) payload[i];
     if(msg[i] == ' '){
      break;
     }
 }
 msg[i++] = '\0';
 Serial.println(msg);
 int j = 0;
 
 for (; i < length; i++) {
     color[j++] = (char) payload[i];
 }
 color[j] = '\0';
 Serial.println(color);
 
 if(strcmp(msg, "fill") == 0){
  if(strcmp(color, "beer") == 0){
    FillBeerBottles();
    client.publish(toCarTopic, "CarGOBottle");
    lastColor = "beer";
  }
  else{
    FillBottles((RgbColor)strToColor(color));
    client.publish(toCarTopic, "CarGOBottle");
    lastColor = "else"; 
  }
 }
 if(strcmp(msg, "empty") == 0){
  if(strcmp(lastColor, "beer") == 0){
   EmptyBeerBottles();
   client.publish(toCarTopic, "CarGOContainer");
  }
  else{
    EmptyBottles();
    client.publish(toCarTopic, "CarGOContainer");  
  }
 }
 if(strcmp(msg, "start") == 0){
  digitalWrite(16, HIGH);
  digitalWrite(5, LOW);
 }
 if(strcmp(msg, "stop") == 0){
  digitalWrite(16, LOW);
  digitalWrite(5, HIGH);
 }
}

void loop() {
 reconnect();
 heartbeat();
 client.loop();
}
