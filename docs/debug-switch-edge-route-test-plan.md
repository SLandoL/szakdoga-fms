# Debug kapcsoló eseményalapú működésének tesztterve

## Cél

A teszt célja annak igazolása, hogy a `ResetPos` fizikai debug kapcsoló nem szintalapú útvonalállítást végez, hanem állapotváltozáskor egyszeri vezérlési eseményt vált ki.

Elvárt működés:

```text
ResetPos kapcsoló átvált
-> a kocsi következő megállója a gyár legyen
```

Nem elvárt működés:

```text
ResetPos True  -> tartós Container állapot
ResetPos False -> tartós Factory állapot
```

## Érintett fájlok

- `tankesp/tankesp.ino`: a meglévő kapcsolóolvasás és MQTT publikálás helye
- `Kocsi/example/car.py`: a kocsi route state kezelésének helye

## Tesztesetek

### DBG-T1 - ResetPos OFF -> ON él

Lépések:

1. Indítsd el az MQTT brokert.
2. Indítsd el a tank ESP-t.
3. Indítsd el a kocsi vezérlőkódját.
4. Kapcsold át a `ResetPos` kapcsolót OFF -> ON irányba.

Elvárt eredmény:

- a tank ESP publikál a `ResetPos` topicra,
- a kocsi konzolján megjelenik: `Debug switch event: next stop forced to factory`,
- a kocsi következő megállónál gyári állapotként viselkedik,
- nem keletkezik diagnosztikai hiba emiatt.

### DBG-T2 - ResetPos ON -> OFF él

Lépések:

1. Az előző teszt után kapcsold vissza a `ResetPos` kapcsolót ON -> OFF irányba.

Elvárt eredmény:

- ugyanaz a debug esemény fut le,
- a kapcsoló OFF állapota nem jelent külön `Factory` route state-et,
- a kocsi következő megállója ismét a gyár lesz.

### DBG-T3 - Tartós kapcsolóállás

Lépések:

1. Hagyd a kapcsolót változatlan állapotban.
2. Figyeld a kocsi konzolját és az MQTT üzeneteket.

Elvárt eredmény:

- nincs ismételt route state felülírás,
- nincs folyamatos debug parancs,
- a kocsi normál állapotgépe tovább tud működni.

### DBG-T4 - Extra stop működés regressziós teszt

Lépések:

1. Kapcsold a `StopLeft` és `StopRight` kapcsolókat.
2. Ellenőrizd az extra megállóknál történő várakozást.
3. Ezután használd a `ResetPos` debug kapcsolót.

Elvárt eredmény:

- `StopLeft` és `StopRight` továbbra is az extra megállók működését befolyásolja,
- a `ResetPos` nem írja át ezek jelentését,
- a debug kapcsoló csak a következő gyári célra kényszerítést végzi.

### DBG-T5 - Dashboard regressziós teszt

Lépések:

1. Debug kapcsoló használata közben figyeld a diagnosztikai dashboardot.

Elvárt eredmény:

- a debug kapcsoló nem jelenik meg diagnosztikai hibaként,
- nem keletkezik új `FaultData`,
- nem jelenik meg új dashboard kapcsolópanel,
- a meglévő RCA és RFID megjelenítés változatlan marad.

## Dokumentálandó eredmények

Minden teszthez érdemes rögzíteni:

- kapcsolóállás-változás iránya,
- MQTT Explorer képernyőkép a `ResetPos` üzenetről,
- kocsi konzolkimenet,
- a kocsi tényleges következő megállója,
- dashboard regressziós képernyőkép.
