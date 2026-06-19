# 4. fejlesztési szakasz: debug kapcsoló eseményalapú útvonalvezérlése

## Kiinduló helyzet

A 4. fejlesztési szakasz célja nem új fizikai kapcsoló-alrendszer létrehozása volt. A maketten a kapcsolók már léteznek, és a meglévő kód is kezeli őket:

- a `tankesp/tankesp.ino` olvassa a kapcsolótábla bemeneteit,
- a kocsi oldali `Kocsi/example/car.py` feliratkozik a `StopLeft`, `StopRight` és `ResetPos` topicokra,
- az extra megálló kapcsolók logikája már a kocsi állapotgépében szerepel.

Ezért a javított fejlesztés nem vezet be külön switch ESP-t, nem hoz létre új dashboard kapcsolópanelt, és nem helyezi át a kapcsolókezelést a diagnosztikai service-be.

## Megvalósított változás

A módosítás kizárólag a tényleges útvonalvezérlési helyen, a kocsi állapotgépében történt:

- módosított fájl: `Kocsi/example/car.py`

A korábbi működésben a `ResetPos` topic szintalapú jelentést kapott:

```text
ResetPos True  -> state = Container
ResetPos False -> state = Factory
```

Ez nem felelt meg a pontosított működésnek, mert a debug kapcsoló célja nem két külön állapot közötti választás, hanem egy esemény kiváltása.

Az új működés:

```text
ResetPos bármely állapotváltozása
-> egyszeri debug esemény
-> a kocsi következő megállója a gyár legyen
```

A kódban ezt a `force_next_stop_factory()` függvény valósítja meg. A függvény a route state-et `States.Container` állapotba állítja, mert a meglévő állapotgépben a `States.Container` állapothoz tartozó következő megálló a gyár. Ez minimális módosítással illeszkedik a már meglévő állapotgéphez.

## Miért került a logika a kocsi kódjába?

A debug kapcsoló nem diagnosztikai hiba, hanem vezérlési esemény. Emiatt nem a `DiagnoseService` vagy a dashboard feladata kezelni. A diagnosztikai service feladata továbbra is a hibák értelmezése és a dashboard számára előállított állapot biztosítása. Az AGV útvonalállapotát a kocsi vezérlőkódja kezeli, ezért a debug kapcsoló eseményét is ott kell feldolgozni.

## Megtartott meglévő működés

Nem változott:

- `StopLeft` kapcsoló kezelése,
- `StopRight` kapcsoló kezelése,
- extra megállóknál történő várakozás,
- RFID diagnosztika,
- dashboard modellek,
- DiagnoseService API,
- tank ESP kapcsolóolvasási alaplogikája.

## Szakdolgozatba beemelhető szöveg

A negyedik fejlesztési szakasz során a már meglévő fizikai kapcsolók közül a debug kapcsoló működését pontosítottam. A kapcsoló korábbi értelmezése szintalapú volt: a kapcsoló `True` és `False` állapota két külön útvonalállapotot állított be. Ez a működés nem volt megfelelő, mert a debug kapcsoló célja nem tartós üzemmód beállítása, hanem egy egyszeri vezérlési esemény kiváltása.

A módosítás után a debug kapcsoló eseményalapú működést kapott. A kocsi vezérlése a `ResetPos` topicra érkező bármely állapotváltozást ugyanúgy kezeli: a következő megállót a gyárra kényszeríti. Ez azt jelenti, hogy nem az számít, hogy a kapcsoló fel- vagy lekapcsolt állapotban van, hanem az, hogy történt-e kapcsolóváltás. Így egy fizikai átkapcsolás pontosan egy vezérlési eseményt okoz, és a kapcsoló tartós állása nem írja felül folyamatosan az útvonalvezérlést.

A fejlesztés során nem került bevezetésre új kapcsoló-ESP vagy új dashboard kapcsolópanel, mivel a kapcsolók már a meglévő hardver- és szoftverstruktúrában szerepeltek. A módosítás célzottan ott történt, ahol az AGV útvonalállapota ténylegesen él: a kocsi vezérlőkódjában.
