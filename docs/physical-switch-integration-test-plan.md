# Fizikai kapcsolók integrációs tesztterve

## Előfeltételek

- MQTT broker elérhető.
- `DiagnoseService` fut.
- `DiagnoseDashboard` fut.
- A 3. fejlesztési szakasz RFID heartbeat és reader státusz logikája elérhető.
- Fizikai ESP teszt esetén a `switchesp/switchesp.ino` firmware fel van töltve, a pin kiosztás hardver szerint ellenőrizve.

## MQTT topicok

```text
PhysicalSwitch/TankPower
PhysicalSwitch/RfidTankPower
PhysicalSwitch/RfidWarehousePower
Agv/Switch/ExtraStop1
Agv/Switch/ExtraStop2
Agv/Switch/Debug
Agv/Command/ForceNextStop
```

## Kézi MQTT tesztek

### SW-T1 - Tartály táp megszakítása

Publikált üzenet:

```json
Topic: PhysicalSwitch/TankPower
{
  "switchId": "TankPower",
  "state": false,
  "changed": true,
  "timestampMs": 1000
}
```

Elvárt eredmény:

- `AramTartaly` diagnosztikai jel hibás lesz.
- A dashboardon a tartály áramellátási ág hibát jelez.
- A kapcsoló snapshotban a `TankPower` állapota ismert és `state=false`.

### SW-T2 - Tartály táp visszakapcsolása

```json
Topic: PhysicalSwitch/TankPower
{
  "switchId": "TankPower",
  "state": true,
  "changed": true,
  "timestampMs": 2000
}
```

Elvárt eredmény:

- A kapcsoló overlay már nem tartja fenn az `AramTartaly` hibát.
- Ha nincs más tartályhiba, az állapot vissza tud állni `WORKING`-ra.

### SW-T3 - Tank oldali RFID olvasó táp megszakítása

```json
Topic: PhysicalSwitch/RfidTankPower
{
  "switchId": "RfidTankPower",
  "state": false,
  "changed": true,
  "timestampMs": 3000
}
```

Elvárt eredmény:

- `KommRfidUp` hibás lesz.
- `GyarRfidOlv` nem önálló mért hiba, hanem az RCA logika szerint legfeljebb következmény.
- Az RFID státusz összefoglaló jelzi, hogy a tank oldali RFID táp megszakadt.

### SW-T4 - Raktár oldali RFID olvasó táp megszakítása

```json
Topic: PhysicalSwitch/RfidWarehousePower
{
  "switchId": "RfidWarehousePower",
  "state": false,
  "changed": true,
  "timestampMs": 4000
}
```

Elvárt eredmény:

- `KommRfidUp` hibás lesz.
- Az RFID státusz összefoglaló jelzi, hogy a raktár oldali RFID táp megszakadt.

### AGV-T1 - ExtraStop1 engedélyezése

```json
Topic: Agv/Switch/ExtraStop1
{
  "switchId": "ExtraStop1",
  "enabled": true,
  "changed": true,
  "timestampMs": 5000
}
```

Elvárt eredmény:

- Nem keletkezik diagnosztikai hiba.
- A kapcsoló snapshotban `ExtraStop1` engedélyezett.
- Az AGV útvonalvezérlés ezt konfigurációs bemenetként használhatja.

### AGV-T2 - ExtraStop2 engedélyezése

```json
Topic: Agv/Switch/ExtraStop2
{
  "switchId": "ExtraStop2",
  "enabled": true,
  "changed": true,
  "timestampMs": 6000
}
```

Elvárt eredmény:

- Nem keletkezik diagnosztikai hiba.
- A kapcsoló snapshotban `ExtraStop2` engedélyezett.

### DBG-T1 - Debug kapcsoló OFF -> ON

```json
Topic: Agv/Switch/Debug
{
  "switchId": "Debug",
  "state": true,
  "changed": true,
  "event": "ForceNextStopFactory",
  "timestampMs": 7000
}
```

Elvárt eredmény:

- Nem keletkezik diagnosztikai hiba.
- A backend publikál az `Agv/Command/ForceNextStop` topicra.
- A publikált parancsban `nextStop = Factory`.

### DBG-T2 - Debug kapcsoló ON -> OFF

```json
Topic: Agv/Switch/Debug
{
  "switchId": "Debug",
  "state": false,
  "changed": true,
  "event": "ForceNextStopFactory",
  "timestampMs": 8000
}
```

Elvárt eredmény:

- Ugyanúgy egyszeri `ForceNextStopFactory` parancs keletkezik.
- A kapcsoló állása nem szintként, hanem állapotváltozásként számít.

### DBG-T3 - Debug kapcsoló változatlan állapotban

```json
Topic: Agv/Switch/Debug
{
  "switchId": "Debug",
  "state": false,
  "changed": false,
  "event": "ForceNextStopFactory",
  "timestampMs": 9000
}
```

Elvárt eredmény:

- Nem kell új AGV parancsnak keletkeznie.
- A periodikus státuszpublikálás nem írja felül folyamatosan az útvonalvezérlést.

## Fizikai ESP tesztek

1. ESP indítás után minden kapcsoló státusza periodikusan megjelenik MQTT-n.
2. Egy kapcsoló átbillentésekor egyetlen `changed=true` üzenet jelenik meg.
3. Kapcsolópattogás esetén a debounce miatt nem keletkezik több gyors egymásutáni esemény.
4. Debug kapcsoló esetén mindkét élváltás parancsot generál.
5. Extra stop kapcsolók nem állítanak hibát.

## Dokumentálandó eredmények

Minden tesztnél rögzítendő:

- tesztazonosító,
- MQTT topic,
- payload,
- várt eredmény,
- tényleges eredmény,
- sikeres/sikertelen,
- dashboard képernyőkép,
- MQTT Explorer képernyőkép, ha elérhető.
