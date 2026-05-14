# RFID diagnosztika tesztterv

Ez a tesztterv a 3. fejlesztési szakaszhoz tartozik. A cél annak ellenőrzése, hogy az RFID ESP heartbeat, a reader státuszok, a cargo üzenetek, a DiagnoseService állapotmodell, az RCA integráció és a dashboard panel együtt helyesen működnek-e.

## Előkészítés

1. Indítsd el az MQTT brokert.
2. Indítsd el a `DiagnoseService` alkalmazást.
3. Indítsd el a `DiagnoseDashboard` alkalmazást.
4. Nyisd meg a dashboardot.
5. Indítsd el az RFID ESP-t, vagy MQTT Explorerrel / mosquitto_pub-bal szimuláld a topicokat.

## Tesztesetek

| Azonosító | Beavatkozás | Elvárt eredmény |
| --- | --- | --- |
| RFID-T1 | ESP fut, 2 másodpercenként `RFID/Heartbeat` érkezik, mindkét reader friss és `ok = true`, cargo match `true` | `KommRfidUp = WORKING`, `GyarRfidOlv = WORKING`, dashboardon ESP online, mindkét reader működik, cargo egyezik |
| RFID-T2 | ESP leállítása vagy MQTT kapcsolat megszakítása | 6 másodperc körüli timeout után `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = CONSEQUENCE` vagy nem mért hiba |
| RFID-T3 | Tank reader státusz `ok = false` | `KommRfidUp = ROOTFAULT`, tank reader nem működik, `GyarRfidOlv` nem lehet önálló gyökérhiba |
| RFID-T4 | Warehouse reader státusz `ok = false` | `KommRfidUp = ROOTFAULT`, warehouse reader nem működik, `GyarRfidOlv` nem lehet önálló gyökérhiba |
| RFID-T5 | Mindkét reader `ok = true`, cargo match `true` | Nincs RFID gyökérhiba, a dashboardon rakományegyezés: egyezik |
| RFID-T6 | Mindkét reader `ok = true`, cargo match `false` | `GyarRfidOlv = ROOTFAULT`, `KommRfidUp = WORKING`, dashboardon rakományegyezés: nem egyezik |
| RFID-T7 | Először cargo mismatch, majd az egyik reader kiesik | A gyökérhiba váltson `KommRfidUp`-ra; a korábbi rakományhiba ne maradjon önálló root fault |
| RFID-T8 | ESP újraindítása | Rövid timeout után visszaáll online állapotra, ha új heartbeat és reader státusz érkezik |
| RFID-T9 | Régi cargo adat bent marad, de heartbeat megszűnik | Timeout után ne látszódjon jó állapotnak; `KommRfidUp` legyen a diagnosztikai gyökérhiba |
| RFID-T10 | Hiba megszüntetése: heartbeat friss, mindkét reader ok, cargo match true | Dashboard és RCA állapot is álljon vissza `WORKING` állapotra |

## Kézi MQTT példaüzenetek

### Normál működés

```bash
mosquitto_pub -h 192.168.0.100 -t RFID/Heartbeat -m '{"deviceId":"rfid-esp","uptimeMs":1000,"wifiConnected":true,"mqttConnected":true,"tankReaderOk":true,"warehouseReaderOk":true}'
mosquitto_pub -h 192.168.0.100 -t RFID/TankReader/Status -m '{"reader":"tank","ok":true,"lastCheckMs":1000,"errorCode":20}'
mosquitto_pub -h 192.168.0.100 -t RFID/WarehouseReader/Status -m '{"reader":"warehouse","ok":true,"lastCheckMs":1000,"errorCode":20}'
mosquitto_pub -h 192.168.0.100 -t RFID/CargoMatch -m '{"match":true,"tankCargoId":"Xanax","warehouseCargoId":"Xanax","timestampMs":1000}'
```

### Rakományeltérés

```bash
mosquitto_pub -h 192.168.0.100 -t RFID/Heartbeat -m '{"deviceId":"rfid-esp","uptimeMs":2000,"wifiConnected":true,"mqttConnected":true,"tankReaderOk":true,"warehouseReaderOk":true}'
mosquitto_pub -h 192.168.0.100 -t RFID/TankReader/Status -m '{"reader":"tank","ok":true,"lastCheckMs":2000,"errorCode":20}'
mosquitto_pub -h 192.168.0.100 -t RFID/WarehouseReader/Status -m '{"reader":"warehouse","ok":true,"lastCheckMs":2000,"errorCode":20}'
mosquitto_pub -h 192.168.0.100 -t RFID/CargoMatch -m '{"match":false,"tankCargoId":"Xanax","warehouseCargoId":"Aspirin","timestampMs":2000}'
```

### Tank olvasóhiba

```bash
mosquitto_pub -h 192.168.0.100 -t RFID/Heartbeat -m '{"deviceId":"rfid-esp","uptimeMs":3000,"wifiConnected":true,"mqttConnected":true,"tankReaderOk":false,"warehouseReaderOk":true}'
mosquitto_pub -h 192.168.0.100 -t RFID/TankReader/Status -m '{"reader":"tank","ok":false,"lastCheckMs":3000,"errorCode":0}'
mosquitto_pub -h 192.168.0.100 -t RFID/WarehouseReader/Status -m '{"reader":"warehouse","ok":true,"lastCheckMs":3000,"errorCode":20}'
```

### Heartbeat timeout

Ehhez ne küldj több `RFID/Heartbeat` és reader status üzenetet. A backendnek a timeout után kommunikációs RFID hibát kell jeleznie.

## Dokumentálandó eredmények

Minden tesztnél érdemes rögzíteni:

- teszt azonosító,
- kiindulási állapot,
- beavatkozás vagy MQTT payload,
- várt eredmény,
- tényleges eredmény,
- sikeres / sikertelen státusz,
- dashboard képernyőkép,
- megjegyzés.

## Build / smoke ellenőrzés

Merge előtt javasolt:

```bash
dotnet build DiagnoseService/DiagnoseService/DiagnoseService.csproj
dotnet build DiagnoseDashboard/DiagnoseDashboard/DiagnoseDashboard.csproj
```

A jelen repository technikai adósságai miatt továbbra is várhatók korábbi warningok, például `.NET 5.0` EOL, `Refit` sérülékenységi warning vagy `TopicFilterBuilder` obsolete warning. Ezek nem ehhez az RFID fejlesztési szakaszhoz tartozó logikai hibák.
