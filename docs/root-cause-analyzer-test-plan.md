# Tesztterv – RootCauseAnalyzer

A tesztek célja annak ellenőrzése, hogy az új gyökérhiba-elemző logika nem pusztán prioritás alapján jelöl gyökérhibát, hanem az explicit hibatérkép szerinti aktív szülő-gyermek kapcsolatokat is figyelembe veszi.

## Tesztesetek

| Azonosító | Kiindulási állapot / bemenet | Elvárt eredmény | Megjegyzés |
|---|---|---|---|
| RCA-T1 | Nincs aktív hiba | Nincs `ROOTFAULT` | Alapállapot ellenőrzése |
| RCA-T2 | `KommRendszer = FAULT` | Csak `KommRendszer = ROOTFAULT` | A rendszerkommunikáció legfelsőbb hiba |
| RCA-T3 | MQTT/központi kommunikáció hibás, `KommKozpont = FAULT` | Csak `KommKozpont = ROOTFAULT` | A központ hibája elfedi az alatta lévő kommunikációs tüneteket |
| RCA-T4 | `KommKocsi = FAULT` | Csak `KommKocsi = ROOTFAULT` | Kocsiág önálló gyökérhibája |
| RCA-T5 | `KommTartaly = FAULT` | Csak `KommTartaly = ROOTFAULT` | Tartályág önálló gyökérhibája |
| RCA-T6 | `KommRfidUp = FAULT` | Csak `KommRfidUp = ROOTFAULT` | RFID kommunikációs hiba önállóan aktív |
| RCA-T7 | `GyarRfidOlv = FAULT` | Csak `GyarRfidOlv = ROOTFAULT` | RFID olvasási/rakományegyezési hiba kommunikációs hiba nélkül |
| RCA-T8 | `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | Csak `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = FAULT` | Kommunikációs hiba erősebb ok, az olvasási hiba következmény |
| RCA-T9 | `KommKozpont = FAULT`, `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | Csak `KommKozpont = ROOTFAULT` | Központi kommunikációs hiba a legmagasabb aktív ok |
| RCA-T10 | `KommTartaly = FAULT`, `AramTartaly = FAULT` | Csak `KommTartaly = ROOTFAULT`, `AramTartaly = FAULT` | Aktív szülő esetén a gyermek nem lesz külön gyökérhiba |
| RCA-T11 | Előző ciklusban `ROOTFAULT`, következő ciklusban nincs aktív hiba | Nincs `ROOTFAULT`, LED kikapcsol | Ellenőrzi a `ResetRootFaults` működését |

## Kézi ellenőrzés javasolt menete

1. Indítsd el a `DiagnoseService` és `DiagnoseDashboard` projekteket.
2. Nyisd meg a dashboardot.
3. MQTT Explorerrel vagy a meglévő diagnosztikai API-n keresztül állítsd a tesztelt hibajeleket aktívra.
4. Várj legalább egy dashboard frissítési ciklust.
5. Ellenőrizd, hogy csak az elvárt hiba jelenik meg gyökérhibaként.
6. Készíts képernyőképet a sikeres állapotról.
7. Állítsd vissza a hibát, majd ellenőrizd, hogy a `ROOTFAULT` jelölés megszűnik.

## Szakdolgozati dokumentációhoz rögzítendő mezők

Minden lefuttatott tesztnél érdemes az alábbiakat rögzíteni:

- teszt azonosítója,
- generált hiba vagy beavatkozás,
- elvárt gyökérhiba,
- tényleges dashboard állapot,
- sikeres/sikertelen eredmény,
- megjegyzés,
- képernyőkép hivatkozása.
