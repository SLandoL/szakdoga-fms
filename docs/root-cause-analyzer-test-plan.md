# Tesztterv – RootCauseAnalyzer

A tesztek célja annak ellenőrzése, hogy az új gyökérhiba-elemző logika nem pusztán prioritás alapján jelöl gyökérhibát, hanem az explicit hibatérkép szerinti aktív szülő-gyermek kapcsolatokat is figyelembe veszi.

A PR-bírálat alapján külön ellenőrzési cél lett, hogy az RCA ne állítson mesterségesen `FAULT` állapotba olyan gyermekhibákat, amelyek nem saját diagnosztikai bemenet alapján aktívak. Ez azért fontos, mert a jelenlegi adatmodellben nincs külön `CONSEQUENCE` vagy `DERIVED` állapot, ezért a következményhibák `FAULT`-ként való megjelenítése félrevezető lenne.

## Tesztesetek

| Azonosító | Kiindulási állapot / bemenet | Elvárt eredmény | Megjegyzés |
|---|---|---|---|
| RCA-T1 | Nincs aktív hiba | Nincs `ROOTFAULT` | Alapállapot ellenőrzése |
| RCA-T2 | `KommRendszer = FAULT` | Csak `KommRendszer = ROOTFAULT`; a gyermekhibák nem válnak automatikusan `FAULT` állapotúvá | Legfelső kommunikációs hiba |
| RCA-T3 | MQTT/központi kommunikáció hibás, `KommKozpont = FAULT` | Csak `KommKozpont = ROOTFAULT`; `KommKocsi`, `KommTartaly`, `KommRfidUp` nem lesz automatikusan `FAULT` | Ellenőrzi, hogy nincs túl agresszív propagation |
| RCA-T4 | `KommKozpontUp = FAULT` önállóan | Csak `KommKozpontUp = ROOTFAULT` | A központ elérhetőségi jel önállóan is vizsgálható |
| RCA-T5 | `KommKocsi = FAULT` | Csak `KommKocsi = ROOTFAULT`; `KommKocsiEsp` nem lesz automatikusan `FAULT` | Kocsiág önálló gyökérhibája |
| RCA-T6 | `KommTartaly = FAULT` | Csak `KommTartaly = ROOTFAULT`; `AramTartaly` és `GyarSzalagSzenz` nem lesz automatikusan `FAULT` | Tartályág önálló gyökérhibája |
| RCA-T7 | `KommRfidUp = FAULT` | Csak `KommRfidUp = ROOTFAULT`; `GyarRfidOlv` marad `WORKING`, ha nincs saját bemeneti hibája | RFID kommunikációs hiba önállóan aktív |
| RCA-T8 | `GyarRfidOlv = FAULT` | Csak `GyarRfidOlv = ROOTFAULT` | RFID olvasási/rakományegyezési hiba kommunikációs hiba nélkül |
| RCA-T9 | `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | Csak `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = FAULT` | Kommunikációs hiba erősebb ok, az olvasási hiba mért következmény |
| RCA-T10 | `KommKozpont = FAULT`, `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | Csak `KommKozpont = ROOTFAULT`, az alatta lévő aktív hibák `FAULT` állapotban maradnak | Központi kommunikációs hiba a legmagasabb aktív ok |
| RCA-T11 | `KommTartaly = FAULT`, `AramTartaly = FAULT` | Csak `KommTartaly = ROOTFAULT`, `AramTartaly = FAULT` | Aktív szülő esetén a gyermek nem lesz külön gyökérhiba |
| RCA-T12 | Előző ciklusban `ROOTFAULT`, következő ciklusban nincs aktív hiba | Nincs `ROOTFAULT`, LED kikapcsol | Ellenőrzi a `ResetRootFaults` és a ciklus eleji állapotfrissítés működését |

## Kézi ellenőrzés javasolt menete

1. Indítsd el a `DiagnoseService` és `DiagnoseDashboard` projekteket.
2. Nyisd meg a dashboardot.
3. MQTT Explorerrel vagy a meglévő diagnosztikai API-n keresztül állítsd a tesztelt hibajeleket aktívra.
4. Várj legalább egy dashboard frissítési ciklust.
5. Ellenőrizd, hogy csak az elvárt hiba jelenik meg gyökérhibaként.
6. Ellenőrizd külön, hogy az elvárt módon `WORKING` állapotban maradó gyermekhibák nem váltak-e `FAULT` állapotúvá.
7. Készíts képernyőképet a sikeres állapotról.
8. Állítsd vissza a hibát, majd ellenőrizd, hogy a `ROOTFAULT` jelölés megszűnik.

## Szakdolgozati dokumentációhoz rögzítendő mezők

Minden lefuttatott tesztnél érdemes az alábbiakat rögzíteni:

- teszt azonosítója,
- generált hiba vagy beavatkozás,
- elvárt gyökérhiba,
- elvárt nem aktivált gyermekhibák,
- tényleges dashboard állapot,
- sikeres/sikertelen eredmény,
- megjegyzés,
- képernyőkép hivatkozása.
