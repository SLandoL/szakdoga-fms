# Tesztterv - RootCauseAnalyzer es DiagnoseDashboardService

A tesztek celja annak ellenorzese, hogy az uj gyokerhiba-elemzo logika nem pusztan prioritas alapjan jelol gyokerhibat, hanem az explicit hibaterkep szerinti aktiv szulo-gyermek kapcsolatokat is figyelembe veszi.

Az aktualis verzio tenyleges hierarchikus propagation-t vegez. A propagation nem `FAULT` allapotot ir a gyermekhibakra, hanem kulon `CONSEQUENCE` allapotot hasznal. Igy a rendszer meg tudja kulonboztetni a kozvetlenul mert hibat a magasabb szintu hiba miatt erintett, bizonytalan vagy kovetkezmenykent jelentkezo komponensektol.

Fontos kulonbseg: az analyzer-szintu tesztek kozvetlenul a `RootCauseAnalyzer` osztalyt vizsgaljak tetszolegesen osszeallitott `FaultData` listaval. A service-integracios tesztek a teljes `DiagnoseDashboardService` folyamatot vizsgaljak. A service magasabb szintu kommunikacios hiba eseten tovabbra sem vegez minden also szintu eszkozallapot-lekerdezest, de a kozvetlenul bejovo `DiagnoseData.Data == true` bemeneteket ekkor is meg kell oriznie `FAULT` allapotkent.

## 1. RootCauseAnalyzer egsegtesztek / logikai tesztek

| Azonosito | Kiindulasi allapot / bemenet | Elvart eredmeny | Megjegyzes |
|---|---|---|---|
| RCA-U1 | Nincs aktiv hiba | Nincs `ROOTFAULT`, nincs `CONSEQUENCE` | Alapallapot ellenorzese |
| RCA-U2 | `KommRendszer = FAULT` | `KommRendszer = ROOTFAULT`; kommunikacios ag elemei `CONSEQUENCE` | Legfelsobb kommunikacios hiba propagation tesztje |
| RCA-U3 | `KommKozpont = FAULT` | `KommKozpont = ROOTFAULT`; `KommKocsi`, `KommTartaly`, `KommRfidUp`, `GyarRfidOlv` stb. `CONSEQUENCE` | Kozponti kommunikacios hiba lefele terjed |
| RCA-U4 | `KommKozpontUp = FAULT` onalloan | `KommKozpontUp = ROOTFAULT`; nem irja felul a `KommKozpont` allapotat | A kozpont elerhetosegi jel onalloan is vizsgalhato |
| RCA-U5 | `KommKocsi = FAULT` | `KommKocsi = ROOTFAULT`; `KommKocsiEsp`, `GyarTargoncaSzenz`, `AramKocsi`, `KommTargoncaArammero` = `CONSEQUENCE` | Kocsiag propagation |
| RCA-U6 | `KommTartaly = FAULT` | `KommTartaly = ROOTFAULT`; `AramTartaly`, `GyarSzalagSzenz`, `GyarTartalySzenz`, `KommTartalyArammero` = `CONSEQUENCE` | Tartalyag propagation |
| RCA-U7 | `KommRfidUp = FAULT` | `KommRfidUp = ROOTFAULT`; `GyarRfidOlv = CONSEQUENCE`, ha nincs sajat bemeneti hibaja | RFID kommunikacios hiba kovetkezmenye az RFID olvasasi bizonytalansag |
| RCA-U8 | `GyarRfidOlv = FAULT` | Csak `GyarRfidOlv = ROOTFAULT` | RFID olvasasi/rakomanyegyezesi hiba kommunikacios hiba nelkul |
| RCA-U9 | `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = FAULT` | A propagation nem irhatja felul a mert `FAULT` allapotot `CONSEQUENCE`-re |
| RCA-U10 | `KommKozpont = FAULT`, `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | Csak `KommKozpont = ROOTFAULT`, az alatta levo mert hibak `FAULT` allapotban maradnak | Analyzer-szintu eset, amikor tobb mert hiba egyszerre van a listaban |
| RCA-U11 | `KommTartaly = FAULT`, `AramTartaly = FAULT` | `KommTartaly = ROOTFAULT`, `AramTartaly = FAULT`, a tobbi tartalyagi gyermek `CONSEQUENCE` | Aktiv mert gyermek nem lesz kulon root, de nem is lesz consequence |
| RCA-U12 | Elozo elemzesi korbol `ROOTFAULT` es `CONSEQUENCE` jelolesek maradtak a listaban | `ResetAnalysisStatuses()` utan a `ROOTFAULT` visszaall `FAULT`-ra, a `CONSEQUENCE` visszaall `WORKING`-re | Analyzer API-szintu reset teszt, nem teljes rendszer-reset |

## 2. DiagnoseDashboardService integracios tesztek

| Azonosito | Rendszerszintu bemenet | Elvart eredmeny | Megjegyzes |
|---|---|---|---|
| RCA-S1 | Kocsi es tartaly heartbeat `ONLINE` | `KommKocsi = WORKING`, `KommTartaly = WORKING` | Nem jelenhet meg hamis hiba aktiv eszkozon |
| RCA-S2 | Kocsi heartbeat elmarad, bottle ESP is offline | `KommKocsi = ROOTFAULT`, `KommKocsiEsp = CONSEQUENCE`, ha nincs sajat mért hibajele | A gyerek ESP csak akkor FAULT, ha sajat diagnózisbemenete is hibas |
| RCA-S3 | Kocsi online, bottle ESP heartbeat elmarad | `KommKocsi = WORKING`, `KommKocsiEsp = ROOTFAULT` | Ilyenkor a bottle ESP mar kulon mert kommunikacios hiba |
| RCA-S4 | Tartaly heartbeat elmarad | `KommTartaly = ROOTFAULT`, tartalyagi gyerekek `CONSEQUENCE`, ha nincs sajat mért hibajeluk | Offline szulo mellett csak a nem mert gyerekek kovetkezmenyek |
| RCA-S5 | Tartaly online, `AramTartaly.Data = true` | `AramTartaly = ROOTFAULT` vagy `FAULT`, ha van magasabb mert tartalyagi hiba | Csak online szulo mellett ertelmezett kulon mert hiba |
| RCA-S6 | RFID olvasok nem elerhetok | `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = CONSEQUENCE`, ha `GyarRfidOlv.Data == false` | A rakomanyhiba nem lesz mert hiba, ha nincs sajat bemeneti hibaja |
| RCA-S7 | RFID olvasok mukodnek, rakomany nem egyezik | `GyarRfidOlv = ROOTFAULT` | Valodi gyartasi/olvasasi hiba kommunikacios hiba nelkul |
| RCA-S8 | MQTT / `KommKozpont` hiba, also szinten nincs sajat `Data == true` hiba | `KommKozpont = ROOTFAULT`, also kommunikacios agak `CONSEQUENCE` | A nem mert also hibak kovetkezmenyek maradnak |
| RCA-S9 | Hiba megszunik egy teljes service ciklus utan | Nincs `ROOTFAULT`, nincs `CONSEQUENCE`, LED kikapcsol | A teljes torlest a `DiagnoseDashboardService.ResetFaultStatuses()` es az RCA ujraszamolasa egyutt vegzi |
| RCA-S10 | Heartbeat 15 masodpercnel ritkabb | Az erintett eszkoz `FAULT` vagy `ROOTFAULT` lesz | Az eszkozoknek 15 masodpercen belul kell allapotot kuldeniuk |
| RCA-S11 | `KommKozpont.Data = true` es `GyarRfidOlv.Data = true` | `KommKozpont = ROOTFAULT`, `GyarRfidOlv = FAULT`, a koztes nem mert elemek `CONSEQUENCE` | A mert also hibat nem irhatja felul a propagation |
| RCA-S12 | `KommKocsi` offline es `AramKocsi.Data = true` | `KommKocsi = ROOTFAULT`, `AramKocsi = FAULT`, a tobbi nem mert kocsiagi gyerek `CONSEQUENCE` | Offline szulo mellett is megmarad a sajat mért hiba |
| RCA-S13 | `KommRfidUp.Data = true` es `GyarRfidOlv.Data = true` | `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = FAULT` | Az RFID olvasasi hiba nem valhat consequence-sze, ha sajat bemeneti hibaja van |

## 3. Kezi ellenorzes javasolt menete

1. Inditsd el a `DiagnoseService` es `DiagnoseDashboard` projekteket.
2. Nyisd meg a dashboardot.
3. MQTT Explorerrel vagy a meglevo diagnosztikai API-n keresztul allitsd a tesztelt hibajeleket aktivra.
4. Varj legalabb egy dashboard frissitesi ciklust.
5. Ellenorizd, hogy csak az elvart hiba jelenik meg gyokerhibakent.
6. Ellenorizd, hogy a leszarmazott, de sajat hibajellel nem rendelkezo komponensek `CONSEQUENCE` allapotban jelennek-e meg.
7. Ellenorizd, hogy a sajat bemeneti hibaval rendelkezo gyermekek `FAULT` allapotban maradnak, es nem irodnak felul `CONSEQUENCE` allapotra.
8. Keszits kepernyokepet a sikeres allapotrol.
9. Allitsd vissza a hibat, majd ellenorizd, hogy a `ROOTFAULT` es `CONSEQUENCE` jelolesek megszunnek.

## 4. Szakdolgozati dokumentaciohoz rogzitendo mezok

Minden lefuttatott tesztnel erdemes az alabbiakat rogzitani:

- teszt azonositoja,
- teszt tipusa: analyzer-szintu vagy service-integracios,
- generalt hiba vagy beavatkozas,
- elvart gyokerhiba,
- elvart kovetkezmenyhibak,
- tenyleges dashboard allapot,
- sikeres/sikertelen eredmeny,
- megjegyzes,
- kepernyokep hivatkozasa.
