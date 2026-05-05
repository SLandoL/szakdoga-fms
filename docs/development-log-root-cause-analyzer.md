# Fejlesztési napló – 1. szakasz: diagnosztikai logika rendbetétele

## Cél

A fejlesztési szakasz célja a diagnosztikai logika szétválasztása volt. A kiindulási állapotban a hibaállapotok frissítése, a hierarchikus hibaterjesztés és a gyökérhiba-kijelölés több helyen összefolyt. A `FaultSearch` osztály tartalmazta a statikus hibadefiníciókat és a prioritásalapú gyökérhiba-kijelölést, míg a `Dashboard.razor` közvetlenül meghívta a gyökérhiba-detekciót.

A módosítás célja az volt, hogy a megjelenítési réteg ne végezzen diagnosztikai számítást, hanem csak a kész hibaállapotokat jelenítse meg. A gyökérhiba-elemzés külön, tesztelhető és később bővíthető osztályba került.

## Kiindulási állapot

A `FaultSearch` osztályban a hibatípusok statikus `FaultData` objektumokként szerepeltek. A prioritások és a `ParentId` mezők szinkronizálása szintén itt történt. A régi gyökérhiba-logika két lépésből állt:

1. `RootFaultDetectIndex()` megkereste a legnagyobb prioritású aktív hibát.
2. `RootFaultDetect(...)` minden azonos prioritású hibát `ROOTFAULT` állapotba tett.

Ez működő alap, de szakdolgozati fejlesztéshez korlátozott, mert a prioritás nem írja le egyértelműen az ok-okozati kapcsolatokat. Például ha egy központi kommunikációs hiba és több alatta lévő eszközhiba egyszerre aktív, a rendszernek a magasabb szintű okot kell kiemelnie, nem pedig az összes hasonló prioritású hibát.

## Elvégzett módosítások

### 1. Új `RootCauseAnalyzer` osztály

Létrejött az alábbi fájl:

```text
DiagnoseDashboard/DiagnoseDashboard/Data/RootCauseAnalyzer.cs
```

Az új osztály három fő felelősséget kapott:

```csharp
public void ResetRootFaults(List<FaultData> faults)
public void PropagateFaults(List<FaultData> faults)
public List<FaultData> DetectRootCauses(List<FaultData> faults)
```

A `ResetRootFaults` visszaállítja a korábban kijelölt `ROOTFAULT` állapotokat `FAULT` állapotba. Erre azért van szükség, mert minden új diagnosztikai ciklusban újra kell számolni, mely hibák tekinthetők valódi gyökérhibának.

A `PropagateFaults` a bírálat alapján szándékosan nem módosítja a `FaultStatus` mezőt. A jelenlegi adatmodellben csak `WORKING`, `FAULT` és `ROOTFAULT` állapot létezik. Ha az elemző a következményként feltételezett hibákat is `FAULT` állapotba írná, akkor a dashboardon nem lenne elkülöníthető a ténylegesen mért hiba és a származtatott következményhiba. Ezért az első RCA-refaktorban a hierarchia nem új hibákat aktivál, hanem a `DetectRootCauses` döntéséhez ad ok-okozati kontextust.

A `DetectRootCauses` az aktív hibák közül azokat jelöli `ROOTFAULT` állapotúra, amelyeknek nincs aktív hibás őse az explicit hibatérképben.

### 2. Explicit hibatérkép bevezetése

A korábbi `ParentId`-alapú, prioritásszintre mutató logika helyett kódszintű hibatérkép került bevezetésre. Ez szándékos tervezési döntés volt: első fejlesztési lépésként nem történt adatbázis-migráció, mert az nagyobb kockázatot jelentene. A hibatérkép így gyorsan módosítható és szakmailag jól indokolható.

A `parentMap` típusa `Dictionary<string, string?>`, mert a legfelső szintű hibáknak nincs szülője.

Példa az új logikára:

```csharp
{ "KommKozpont", "KommRendszer" }
{ "KommKozpontUp", "KommKozpont" }
{ "KommKocsi", "KommKozpont" }
{ "KommTartaly", "KommKozpont" }
{ "KommRfidUp", "KommKozpont" }
{ "GyarRfidOlv", "KommRfidUp" }
```

Fontos RFID döntés: a `GyarRfidOlv` hibát a `KommRfidUp` alá tettem. Ennek oka, hogy ha egyszerre aktív az RFID kommunikációs hiba és az RFID olvasási/rakományegyezési hiba, akkor a kommunikációs hiba erősebb, magasabb szintű ok. Ha viszont csak a `GyarRfidOlv` aktív, akkor önálló gyökérhiba marad.

A `KommKozpontUp` kezelését is pontosítottam. Korábban a `DiagnoseAnalyse()` ezt a bemeneti hibát automatikusan `KommKozpont` hibára képezte le. A javítás után a `KommKozpontUp` önálló diagnosztikai jelként is megjelenhet, ha maga az MQTT státusz és a központ általános kommunikációs állapota nem hibás. Ha viszont az MQTT nem elérhető vagy a `KommKozpont` hibás, akkor a magasabb szintű `KommKozpont` lesz a gyökérhiba.

### 3. RCA bekötése a `DiagnoseDashboardService` osztályba

A `DiagnoseDashboardService` konstruktorába bekerült a `RootCauseAnalyzer` függőség. A `GetDiagnosesAsync()` metódus a diagnózisok lekérése és a hibaállapotok frissítése után meghívja az új RCA-folyamatot:

```csharp
RunRootCauseAnalysis();
```

A `RunRootCauseAnalysis()` lépései:

1. korábbi gyökérhiba-jelölések törlése,
2. hierarchikus terjesztési lépés meghívása, amely jelenleg nem ír állapotot,
3. gyökérhibák kiválasztása,
4. LED állapot beállítása aszerint, hogy van-e aktív gyökérhiba.

Ezzel a gyökérhiba-meghatározás a megjelenítési rétegből az üzleti/diagnosztikai logika rétegébe került.

### 4. Állapotfrissítés és hierarchikus terjesztés szétválasztása

A bírálat alapján a `TreeSearchF` és `TreeSearchW` metódusokat egyszerűsítettem. Korábban ezek nemcsak a megadott hibát állították `FAULT` vagy `WORKING` állapotba, hanem a `ParentId` és prioritás alapján gyermekhibák állapotát is módosították. Ez ugyan működőképes kaszkádot adott, de összemosta a ténylegesen mért hibát a következményhibával.

Az új működés:

```csharp
TreeSearchF("KommRfidUp") -> csak a KommRfidUp lesz FAULT
TreeSearchW("KommRfidUp") -> csak a KommRfidUp lesz WORKING
```

A diagnosztikai ciklus elején a service visszaállítja a hibákat `WORKING` állapotba, majd csak az aktuálisan mért vagy explicit módon ellenőrzött hibákat állítja `FAULT` állapotba. Ez csökkenti a beragadt, korábbi ciklusból származó hibajelölések kockázatát.

### 5. Dashboard egyszerűsítése

A `Dashboard.razor` fájlból eltávolításra került a közvetlen hívás:

```csharp
faultSearch.RootFaultDetect(faultSearch.RootFaultDetectIndex());
```

A dashboard továbbra is a `faultSearch.faultDatas` aktuális állapotát jeleníti meg, de már nem ő számolja ki a gyökérhibát. Ez fontos architekturális javítás, mert a UI réteg felelőssége így a megjelenítésre korlátozódik.

### 6. Dependency injection regisztráció

A `Startup.cs` fájlban regisztrálva lett az új osztály:

```csharp
services.AddScoped<RootCauseAnalyzer>();
```

Erre azért volt szükség, hogy a `DiagnoseDashboardService` konstruktoron keresztül megkapja az RCA modult.

## Új gyökérhiba-szabály

Egy aktív hiba akkor lesz gyökérhiba, ha:

1. `FaultStatus == FAULT` vagy korábbi ciklusból `ROOTFAULT`,
2. nincs olyan aktív szülője az explicit hibatérkép szerint, amely szintén hibás,
3. ezért ő a legmagasabb szintű aktív hiba az adott hibaágban.

Példák:

| Aktív hibák | Elvárt gyökérhiba |
|---|---|
| `KommKozpont`, `KommKocsi`, `KommTartaly`, `KommRfidUp` | `KommKozpont` |
| `KommKozpontUp` | `KommKozpontUp` |
| `KommRfidUp` | `KommRfidUp` |
| `KommRfidUp`, `GyarRfidOlv` | `KommRfidUp` |
| `GyarRfidOlv` | `GyarRfidOlv` |
| `KommTartaly`, `AramTartaly` | `KommTartaly` |

## Miért jobb ez a megoldás?

A módosítás után a rendszer nem pusztán prioritás alapján választ gyökérhibát, hanem a hibák közötti explicit ok-okozati kapcsolatokat is figyelembe veszi. Ez jobban illeszkedik egy ipari hibadiagnosztikai rendszer céljához, mert a kezelőt nem az összes aktív tünet felé irányítja, hanem a legfelsőbb aktív okot emeli ki.

A fejlesztés további előnyei:

- a UI-ból kikerült a diagnosztikai döntési logika,
- a hibatérkép külön helyen módosítható,
- az adatbázisséma nem változott,
- az állapotfrissítés már nem terjeszt automatikusan gyermekhibákat,
- a mért hiba és a következményhiba nem mosódik össze `FAULT` állapotként,
- az RFID kommunikációs és RFID olvasási hiba viszonya tisztábban kezelhető,
- az RCA később unit tesztekkel vagy részletesebb szabályokkal bővíthető.

## Korlátok és következő lépések

Ez a fejlesztési szakasz továbbra sem vezet be külön `CONSEQUENCE` vagy `DERIVED` állapotot. Emiatt a következményhibák vizuális megjelenítésére még nincs külön státusz. Ez tudatos kompromisszum: a jelenlegi háromállapotú modellben biztonságosabb nem származtatott hibákat `FAULT` állapotként megjeleníteni.

Következő lehetséges lépések:

1. unit tesztek írása a `RootCauseAnalyzer` osztályra,
2. külön következményhiba-állapot vagy view model bevezetése,
3. RFID állapotmodell bevezetése,
4. dashboardon külön gyökérhiba-panel kialakítása,
5. később valódi `ParentFaultId` adatmodell bevezetése adatbázis-migrációval.
