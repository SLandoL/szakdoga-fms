# 3. fejlesztési szakasz: RFID diagnosztika és heartbeat-alapú állapotfelügyelet

## Cél

A harmadik fejlesztési szakasz célja az RFID-alapú rakományellenőrzés diagnosztikai megbízhatóságának javítása volt. A kiindulási rendszerben az RFID ESP két MFRC522 olvasóval kezelte a tank- és raktároldali rakományazonosítókat, majd MQTT-n publikálta az olvasók működési állapotát és a rakományegyezés eredményét.

A korábbi megoldás fő korlátja az volt, hogy az olvasók működőképességének megítélése főként eseményalapú üzenetekből történt. Induláskor az ESP egyszer elküldte, hogy az olvasók működnek, de ha később az ESP lefagyott, elvesztette a Wi-Fi-t, megszakadt az MQTT kapcsolat, vagy egyszerűen nem küldött több üzenetet, akkor a backend nem tudta biztosan eldönteni, hogy az állapot friss-e.

Ezért a fejlesztés fő célja nem egyszerűen az RFID-hibák megjelenítése volt, hanem az alábbi esetek elkülönítése:

1. Az RFID ESP teljesen offline.
2. Az ESP online, de az egyik RFID olvasó nem válaszol.
3. Mindkét olvasó működik, de nincs friss rakományegyezési adat.
4. Mindkét olvasó működik, de a rakományazonosítók nem egyeznek.
5. Mindkét olvasó működik, és a rakományazonosítók egyeznek.

## MQTT topicstruktúra

A régi topicok kompatibilitási okból megmaradtak:

| Topic | Szerep |
| --- | --- |
| `Tank_olvaso_mukodik` | Tank oldali olvasó régi bool állapota |
| `WH_olvaso_mukodik` | Raktár oldali olvasó régi bool állapota |
| `TANK_rakomany` | Tank oldali rakomány régi string payloadja |
| `WH_rakomany` | Raktár oldali rakomány régi string payloadja |
| `Rakomany_egyezes` | Régi bool rakományegyezés |

Ezek mellé bekerültek az új, strukturált topicok:

| Topic | Küldő | Fogadó | Jelentés |
| --- | --- | --- | --- |
| `RFID/Heartbeat` | RFID ESP | DiagnoseService | ESP életjel és összefoglaló reader állapot |
| `RFID/TankReader/Status` | RFID ESP | DiagnoseService | Tank oldali olvasó friss állapota |
| `RFID/WarehouseReader/Status` | RFID ESP | DiagnoseService | Raktár oldali olvasó friss állapota |
| `RFID/TankReader/Cargo` | RFID ESP | DiagnoseService | Tank oldali beolvasott rakomány |
| `RFID/WarehouseReader/Cargo` | RFID ESP | DiagnoseService | Raktár oldali beolvasott rakomány |
| `RFID/CargoMatch` | RFID ESP | DiagnoseService | A két olvasott rakomány egyezése |

## ESP firmware módosítások

A `rfidesp/rfidesp.ino` fájlban a korábbi egyszeri reader státuszpublikálás helyett periodikus diagnosztikai publikálás készült.

### Heartbeat

Az ESP 2 másodpercenként publikál a `RFID/Heartbeat` topicra. A payload tartalmazza:

- `deviceId`,
- `uptimeMs`,
- `wifiConnected`,
- `mqttConnected`,
- `tankReaderOk`,
- `warehouseReaderOk`.

Ez alapján a backend nemcsak explicit hibaüzenetből, hanem az életjel frissességéből is tud következtetni az ESP elérhetőségére.

### Reader státuszok

Az ESP 2 másodpercenként publikálja mindkét RFID olvasó státuszát:

- `RFID/TankReader/Status`,
- `RFID/WarehouseReader/Status`.

A payload tartalmazza az olvasó nevét, az `ok` állapotot, a firmware oldali `lastCheckMs` értéket és az MFRC522 regiszterből olvasott hibakódot. A régi `Tank_olvaso_mukodik` és `WH_olvaso_mukodik` topicokra továbbra is küld üzenetet, de csak kompatibilitási célból.

### Rakományolvasás

A firmware strukturált cargo üzeneteket is küld:

- `RFID/TankReader/Cargo`,
- `RFID/WarehouseReader/Cargo`,
- `RFID/CargoMatch`.

A régi `TANK_rakomany`, `WH_rakomany` és `Rakomany_egyezes` topicok megmaradtak, így a régebbi backend működés nem törik el.

### Nem blokkoló hibafigyelés

A korábbi firmware hiba esetén blokkoló `do-while` ciklusban próbálta újrainicializálni az olvasót. Ez diagnosztikai szempontból problémás, mert egy hibás reader miatt az ESP heartbeat is megállhatott. A módosítás után a reader ellenőrzés ciklikusan történik, és hiba esetén az ESP továbbra is tud heartbeatet és státuszüzeneteket küldeni.

## DiagnoseService módosítások

A backend oldalon új `RfidStatus` modell készült. Ez eltárolja:

- az ESP online állapotát,
- a tank és warehouse reader állapotát,
- a reader státuszok frissességét,
- az utolsó heartbeat időpontját,
- az utolsó reader státuszok időpontját,
- az utolsó cargo olvasás időpontját,
- a két cargo ID-t,
- a cargo match ismertségét és értékét,
- a reader hibakódokat,
- egy rövid diagnosztikai összefoglalót.

A `MQTTSubscriber` most feliratkozik az új strukturált RFID topicokra és továbbra is kezeli a régi topicokat. Az új diagnosztikai döntés timeout alapú:

```text
Ha nincs friss heartbeat:
    KommRfidUp = true
    GyarRfidOlv = false

Ha az ESP online, de bármelyik reader státusza régi vagy false:
    KommRfidUp = true
    GyarRfidOlv = false

Ha az ESP online, mindkét reader friss és ok, de a rakomány nem egyezik:
    KommRfidUp = false
    GyarRfidOlv = true

Ha az ESP online, mindkét reader friss és ok, és a rakomány egyezik:
    KommRfidUp = false
    GyarRfidOlv = false
```

A heartbeat timeout és a reader státusz timeout 6 másodperc. Ez szándékosan nagyobb, mint a 2 másodperces firmware oldali publikálási periódus, így néhány kimaradt üzenet még nem okoz azonnal hibát, de a régi állapot sem marad sokáig tévesen jónak látszó állapotban.

## RCA integráció

A korábban kialakított RCA hierarchiához a mostani fejlesztés illeszkedik:

```text
KommRfidUp
└── GyarRfidOlv
```

A jelentése:

- Ha az RFID ESP vagy valamelyik reader nem megbízható, akkor a gyökérhiba `KommRfidUp`.
- Ilyenkor a `GyarRfidOlv` nem lehet önálló mért rakományhiba, mert a rakományegyezési adat nem megbízható.
- Ha az RFID infrastruktúra működik, de a rakományazonosítók eltérnek, akkor a `GyarRfidOlv` lehet önálló gyökérhiba.

Ez pontosítja az előző fejlesztési szakaszban már kialakított `CONSEQUENCE` modellt.

## Dashboard módosítások

A dashboard külön RFID diagnosztikai panelt kapott. A panel megjeleníti:

- ESP kapcsolat: online / offline,
- utolsó heartbeat relatív ideje,
- tank reader állapot: működik / nem működik / nincs friss adat,
- warehouse reader állapot: működik / nem működik / nincs friss adat,
- reader hibakódok,
- tank és raktár cargo ID,
- cargo match állapot,
- utolsó cargo olvasás ideje,
- backend diagnosztikai összefoglaló.

A dashboard így már nemcsak azt mutatja, hogy RFID hiba van, hanem azt is, hogy az ESP, valamelyik olvasó vagy a rakományegyezési folyamat a valószínű problémaforrás.

## Döntési mátrix

| ESP heartbeat | Tank reader | WH reader | Cargo match | `KommRfidUp` | `GyarRfidOlv` | Gyökérhiba |
| --- | --- | --- | --- | --- | --- | --- |
| nincs / régi | ismeretlen | ismeretlen | ismeretlen | true | false / consequence | `KommRfidUp` |
| friss | false | true | ismeretlen | true | false / consequence | `KommRfidUp` |
| friss | true | false | ismeretlen | true | false / consequence | `KommRfidUp` |
| friss | true | true | nincs adat | false | false | nincs RFID root |
| friss | true | true | false | false | true | `GyarRfidOlv` |
| friss | true | true | true | false | false | nincs |

## Szakdolgozatba beemelhető összefoglaló

A harmadik fejlesztési szakasz célja az RFID-alapú rakományellenőrzés diagnosztikai megbízhatóságának javítása volt. A kiindulási rendszerben az RFID ESP két MFRC522 olvasó segítségével beolvasta a tank- és raktároldali rakományazonosítókat, majd MQTT-n továbbította az olvasók működési állapotát és a rakományegyezés eredményét. A megoldás korlátja az volt, hogy az olvasók működőképességének megítélése főként eseményalapú üzenetekből történt, és nem állt rendelkezésre folyamatos heartbeat vagy frissességvizsgálat. Emiatt a backend nem tudta egyértelműen megkülönböztetni az RFID ESP kiesését, az olvasók meghibásodását és a valódi rakományeltérést.

A fejlesztés során ezért az RFID ESP firmware-e periodikus heartbeat üzenetekkel és külön reader-státusz publikálással egészült ki. A DiagnoseService oldalon új RFID állapotmodell készült, amely eltárolja az utolsó heartbeat, az olvasóállapotok és a rakományolvasások időpontját. A diagnosztikai logika timeout alapján is képes hibát jelezni, tehát nemcsak explicit hibajelzés, hanem a friss állapotüzenetek hiánya esetén is felismeri az RFID kommunikációs problémát. A gyökérhiba-elemzésben az RFID kommunikációs hiba magasabb szintű okként szerepel, míg a rakományeltérés csak akkor jelenik meg önálló gyökérhibaként, ha az ESP és mindkét olvasó működőképes.
