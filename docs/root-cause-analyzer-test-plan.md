# Tesztterv - RootCauseAnalyzer

A tesztek célja annak ellenőrzése, hogy az új gyökérhiba-elemző logika nem pusztán prioritás alapján jelöl gyökérhibát, hanem az explicit hibatérkép szerinti aktív szülő-gyermek kapcsolatokat is figyelembe veszi.

Az aktuális verzió már tényleges hierarchikus propagation-t is végez. A propagation nem `FAULT` állapotot ír a gyermekhibákra, hanem külön `CONSEQUENCE` állapotot használ. Így a rendszer meg tudja különböztetni a közvetlenül mért hibát a magasabb szintű hiba miatt érintett, bizonytalan vagy következményként jelentkező komponensektől.

## Tesztesetek

| Azonosító | Kiindulási állapot / bemenet | Elvárt eredmény | Megjegyzés |
|---|---|---|---|
| RCA-T1 | Nincs aktív hiba | Nincs `ROOTFAULT`, nincs `CONSEQUENCE` | Alapállapot ellenőrzése |
| RCA-T2 | `KommRendszer = FAULT` | `KommRendszer = ROOTFAULT`; az alatta lévő kommunikációs ág elemei `CONSEQUENCE` | Legfelső kommunikációs hiba propagation tesztje |
| RCA-T3 | MQTT/központi kommunikáció hibás, `KommKozpont = FAULT` | `KommKozpont = ROOTFAULT`; `KommKocsi`, `KommTartaly`, `KommRfidUp`, `GyarRfidOlv` stb. `CONSEQUENCE` | Központi kommunikációs hiba lefelé terjed |
| RCA-T4 | `KommKozpontUp = FAULT` önállóan | `KommKozpontUp = ROOTFAULT`; nem írja felül a `KommKozpont` állapotát | A központ elérhetőségi jel önállóan is vizsgálható |
| RCA-T5 | `KommKocsi = FAULT` | `KommKocsi = ROOTFAULT`; `KommKocsiEsp`, `GyarTargoncaSzenz`, `AramKocsi`, `KommTargoncaArammero` = `CONSEQUENCE` | Kocsiág propagation |
| RCA-T6 | `KommTartaly = FAULT` | `KommTartaly = ROOTFAULT`; `AramTartaly`, `GyarSzalagSzenz`, `GyarTartalySzenz`, `KommTartalyArammero` = `CONSEQUENCE` | Tartályág propagation |
| RCA-T7 | `KommRfidUp = FAULT` | `KommRfidUp = ROOTFAULT`; `GyarRfidOlv = CONSEQUENCE`, ha nincs saját bemeneti hibája | RFID kommunikációs hiba következménye az RFID olvasási bizonytalanság |
| RCA-T8 | `GyarRfidOlv = FAULT` | Csak `GyarRfidOlv = ROOTFAULT` | RFID olvasási/rakományegyezési hiba kommunikációs hiba nélkül |
| RCA-T9 | `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = FAULT` | A propagation nem írhatja felül a mért `FAULT` állapotot `CONSEQUENCE`-re |
| RCA-T10 | `KommKozpont = FAULT`, `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | Csak `KommKozpont = ROOTFAULT`, az alatta lévő mért hibák `FAULT` állapotban maradnak | Központi kommunikációs hiba a legmagasabb mért ok |
| RCA-T11 | `KommTartaly = FAULT`, `AramTartaly = FAULT` | `KommTartaly = ROOTFAULT`, `AramTartaly = FAULT`, a többi tartályági gyermek `CONSEQUENCE` | Aktív mért gyermek nem lesz külön root, de nem is lesz consequence |
| RCA-T12 | Előző ciklusban `ROOTFAULT` és `CONSEQUENCE`, következő ciklusban nincs aktív hiba | Nincs `ROOTFAULT`, nincs `CONSEQUENCE`, LED kikapcsol | Ellenőrzi a ciklus eleji állapotfrissítést és resetet |

## Kézi ellenőrzés javasolt menete

1. Indítsd el a `DiagnoseService` és `DiagnoseDashboard` projekteket.
2. Nyisd meg a dashboardot.
3. MQTT Explorerrel vagy a meglévő diagnosztikai API-n keresztül állítsd a tesztelt hibajeleket aktívra.
4. Várj legalább egy dashboard frissítési ciklust.
5. Ellenőrizd, hogy csak az elvárt hiba jelenik meg gyökérhibaként.
6. Ellenőrizd, hogy a leszármazott, de saját hibajellel nem rendelkező komponensek `CONSEQUENCE` állapotban jelennek-e meg.
7. Ellenőrizd, hogy a saját bemeneti hibával rendelkező gyermekek `FAULT` állapotban maradnak, és nem íródnak felül `CONSEQUENCE` állapotra.
8. Készíts képernyőképet a sikeres állapotról.
9. Állítsd vissza a hibát, majd ellenőrizd, hogy a `ROOTFAULT` és `CONSEQUENCE` jelölések megszűnnek.

## Szakdolgozati dokumentációhoz rögzítendő mezők

Minden lefuttatott tesztnél érdemes az alábbiakat rögzíteni:

- teszt azonosítója,
- generált hiba vagy beavatkozás,
- elvárt gyökérhiba,
- elvárt következményhibák,
- tényleges dashboard állapot,
- sikeres/sikertelen eredmény,
- megjegyzés,
- képernyőkép hivatkozása.
