# 4. fejlesztési szakasz: fizikai kapcsolók diagnosztikai és vezérlési integrációja

## Cél

A negyedik fejlesztési szakasz célja nem új fizikai kapcsolók bevezetése, hanem a maketten már meglévő kapcsolók egyértelmű diagnosztikai és vezérlési jelentésének rögzítése a szoftverben.

A kapcsolók három csoportba kerültek:

| Kapcsolócsoport | Kapcsolók | Szoftveres szerep |
| --- | --- | --- |
| Tápmegszakító kapcsolók | `TankPower`, `RfidTankPower`, `RfidWarehousePower` | diagnosztikai hibabemenet |
| AGV útvonal kapcsolók | `ExtraStop1`, `ExtraStop2` | útvonal-konfigurációs bemenet |
| Debug kapcsoló | `Debug` | eseményalapú vezérlési trigger |

A legfontosabb döntés: a tápmegszakító kapcsolók hibát okoznak, az extra megálló kapcsolók nem hibák, a debug kapcsoló pedig állapotváltozásra egyszeri vezérlési eseményt generál.

## MQTT topicstruktúra

### Diagnosztikai tápmegszakítók

```text
PhysicalSwitch/TankPower
PhysicalSwitch/RfidTankPower
PhysicalSwitch/RfidWarehousePower
```

Payload példa:

```json
{
  "switchId": "TankPower",
  "state": false,
  "changed": true,
  "timestampMs": 123456
}
```

Jelentés:

```text
state = true  -> táp engedélyezve
state = false -> táp megszakítva
```

### AGV útvonal kapcsolók

```text
Agv/Switch/ExtraStop1
Agv/Switch/ExtraStop2
```

Payload példa:

```json
{
  "switchId": "ExtraStop1",
  "enabled": true,
  "changed": false,
  "timestampMs": 123456
}
```

### Debug kapcsoló

```text
Agv/Switch/Debug
```

Payload példa:

```json
{
  "switchId": "Debug",
  "state": true,
  "changed": true,
  "event": "ForceNextStopFactory",
  "timestampMs": 123456
}
```

A debug kapcsoló edge-triggered módon működik. Nem az állapot szintje a lényeg, hanem az, hogy történt-e állapotváltozás. Ha igen, a backend az alábbi vezérlési topicra publikál:

```text
Agv/Command/ForceNextStop
```

Payload:

```json
{
  "event": "ForceNextStopFactory",
  "nextStop": "Factory",
  "source": "DebugSwitch",
  "debugState": true,
  "timestampUtc": "..."
}
```

## Backend módosítások

Új fájlok:

- `DiagnoseService/DiagnoseService/Data/PhysicalSwitchStatus.cs`
- `DiagnoseService/DiagnoseService/Data/PhysicalSwitchSnapshot.cs`
- `DiagnoseService/DiagnoseService/Controllers/PhysicalSwitchManager.cs`
- `DiagnoseService/DiagnoseService/Controllers/PhysicalSwitchSubscriber.cs`

A `PhysicalSwitchManager` felelősségei:

1. kapcsolótopicok és kapcsolójelentések nyilvántartása,
2. JSON vagy egyszerű bool payload feldolgozása,
3. kapcsolóállapotok és állapotváltozások tárolása,
4. tápmegszakító kapcsolók diagnosztikai overlay-ként való alkalmazása,
5. debug kapcsoló változásakor `Agv/Command/ForceNextStop` parancs publikálása,
6. dashboard számára snapshot előállítása.

A tápmegszakító kapcsolók diagnosztikai leképezése:

| Kapcsoló | Állapot | Diagnosztikai hatás |
| --- | --- | --- |
| `TankPower` | `state=false` | `AramTartaly = true` |
| `RfidTankPower` | `state=false` | `KommRfidUp = true` |
| `RfidWarehousePower` | `state=false` | `KommRfidUp = true` |

Az RFID tápmegszakítás nem `GyarRfidOlv` hibára képződik, hanem kommunikációs hibára. Ez illeszkedik az előző fejlesztési szakaszhoz, ahol az RFID olvasóhiba és a rakományeltérés szét lett választva.

## Firmware minta

Új fájl:

- `switchesp/switchesp.ino`

Ez egy külön ESP firmware minta a fizikai kapcsolók olvasására. Tartalmazza:

- kapcsoló pin konfigurációt,
- debouncing logikát,
- előző és aktuális állapot tárolását,
- változáskor azonnali MQTT publikálást,
- periodikus állapotpublikálást,
- debug kapcsolónál `ForceNextStopFactory` eseményt.

A pin kiosztás jelenleg mintaérték, a tényleges hardverhez ellenőrizni kell.

## Szakdolgozatba beemelhető összefoglaló

A negyedik fejlesztési szakaszban a maketten már meglévő fizikai kapcsolókat integráltam a diagnosztikai és vezérlési logikába. A kapcsolók egy része tényleges fizikai hibát idéz elő, például a tartály vagy az RFID olvasók tápellátásának megszakításával. Ezeket a rendszer diagnosztikai hibabemenetként kezeli: a tartály tápmegszakítása tartály áramellátási hibaként, az RFID olvasók tápmegszakítása pedig RFID kommunikációs hibaként jelenik meg.

A kapcsolók másik része nem hibát generál, hanem az AGV kocsi útvonalát módosítja. Két kapcsolóval az extra megállóhelyek engedélyezhetők, ezek ezért útvonal-konfigurációs bemenetként szerepelnek. A debug kapcsolót eseményalapú működésre alakítottam ki: a rendszer nem a kapcsoló aktuális szintjére reagál folyamatosan, hanem csak az állapotváltozásra. Állapotváltozáskor egy `ForceNextStopFactory` vezérlési esemény keletkezik, amely a kocsi következő célpontját a gyárra állítja.

## Korlátok

- A tényleges fizikai pin kiosztást a makett hardverén ellenőrizni kell.
- A backend parancsot publikál az AGV vezérlés felé, de az AGV oldali fogadó logikát a tényleges kocsivezérlő szoftverben kell ellenőrizni vagy kiegészíteni.
- A dashboard modell és API interfész elkészült, de a teljes vizuális kapcsolópanel külön UI-finomsításként tovább bővíthető.
