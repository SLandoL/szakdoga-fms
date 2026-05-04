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

A `PropagateFaults` az explicit hibatérkép alapján továbbterjeszti a hibát az alacsonyabb szintű következményhibákra. Ez külön kezeli a hierarchikus következményeket a gyökérhiba-kiválasztástól.

A `DetectRootCauses` az aktív hibák közül azokat jelöli `ROOTFAULT` állapotúra, amelyeknek nincs aktív hibás őse az explicit hibatérképben.

### 2. Explicit hibatérkép bevezetése

A korábbi `ParentId`-alapú, prioritásszintre mutató logika helyett kódszintű hibatérkép került bevezetésre. Ez szándékos tervezési döntés volt: első fejlesztési lépésként nem történt adatbázis-migráció, mert az nagyobb kockázatot jelentene. A hibatérkép így gyorsan módosítható és szakmailag jól indokolható.

Példa az új logikára:

```csharp
{ "KommKozpont", "KommRendszer" }
{ "KommKocsi", "KommKozpont" }
{ "KommTartaly", "KommKozpont" }
{ "KommRfidUp", "KommKozpont" }
{ "GyarRfidOlv", "KommRfidUp" }
```

Fontos RFID döntés: a `GyarRfidOlv` hibát a `KommRfidUp` alá tettem. Ennek oka, hogy ha egyszerre aktív az RFID kommunikációs hiba és az RFID olvasási/rakományegyezési hiba, akkor a kommunikációs hiba erősebb, magasabb szintű ok. Ha viszont csak a `GyarRfidOlv` aktív, akkor önálló gyökérhiba marad.

### 3. RCA bekötése a `DiagnoseDashboardService` osztályba

A `DiagnoseDashboardService` konstruktorába bekerült a `RootCauseAnalyzer` függőség. A `GetDiagnosesAsync()` metódus a diagnózisok lekérése és a hibaállapotok frissítése után meghívja az új RCA-folyamatot:

```csharp
RunRootCauseAnalysis();
```

A `RunRootCauseAnalysis()` lépései:

1. korábbi gyökérhiba-jelölések törlése,
2. hierarchikus hibaterjesztés,
3. gyökérhibák kiválasztása,
4. LED állapot beállítása aszerint, hogy van-e aktív gyökérhiba.

Ezzel a gyökérhiba-meghatározás a megjelenítési rétegből az üzleti/diagnosztikai logika rétegébe került.

### 4. Dashboard egyszerűsítése

A `Dashboard.razor` fájlból eltávolításra került a közvetlen hívás:

```csharp
faultSearch.RootFaultDetect(faultSearch.RootFaultDetectIndex());
```

A dashboard továbbra is a `faultSearch.faultDatas` aktuális állapotát jeleníti meg, de már nem ő számolja ki a gyökérhibát. Ez fontos architekturális javítás, mert a UI réteg felelőssége így a megjelenítésre korlátozódik.

### 5. Dependency injection regisztráció

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
- az RCA később unit tesztekkel vagy részletesebb szabályokkal bővíthető,
- az RFID kommunikációs és RFID olvasási hiba viszonya tisztábban kezelhető.

## Korlátok és következő lépések

Ez a fejlesztési szakasz még nem vezeti ki teljesen a régi `TreeSearchF` és `TreeSearchW` metódusokat. Ezek továbbra is részt vesznek az állapotfrissítésben, de a gyökérhiba-kijelölés már külön modulba került.

Következő lehetséges lépések:

1. a `TreeSearchF` és `TreeSearchW` fokozatos egyszerűsítése,
2. unit tesztek írása a `RootCauseAnalyzer` osztályra,
3. RFID állapotmodell bevezetése,
4. dashboardon külön gyökérhiba-panel kialakítása,
5. később valódi `ParentFaultId` adatmodell bevezetése adatbázis-migrációval.
