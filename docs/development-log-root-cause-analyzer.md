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

A `PropagateFaults` tényleges hierarchikus propagation-t végez. Az aktív, ténylegesen mért hibákból (`FAULT` vagy `ROOTFAULT`) kiindulva végigmegy az explicit hibatérképen, és az alatta lévő, nem mért hibákat `CONSEQUENCE` állapotra állítja. Fontos, hogy a propagation nem írja felül a `FAULT` vagy `ROOTFAULT` állapotot, tehát a mért hibák és a származtatott következmények megkülönböztethetők maradnak.

A `DetectRootCauses` csak a ténylegesen aktív mért hibák közül választ gyökérhibát. A `CONSEQUENCE` állapotú elemek nem lehetnek gyökérhibák, mert ezek nem önálló hibabemenetként jelentkeztek, hanem egy magasabb szintű hiba következményeiként bizonytalan vagy érintett komponensek.

### 2. `CONSEQUENCE` állapot bevezetése

A `FaultStatus` enum bővült:

```csharp
public enum FaultStatus
{
    ROOTFAULT,
    FAULT,
    WORKING,
    CONSEQUENCE
}
```

Az új érték szándékosan a lista végére került, hogy a meglévő `ROOTFAULT`, `FAULT` és `WORKING` numerikus értékei ne tolódjanak el. Ez óvatosabb megoldás, mert a rendszer korábban már használta ezeket az enumértékeket.

Az új állapot jelentése:

```text
CONSEQUENCE = nem közvetlenül mért hiba, hanem egy aktív felsőbb hiba miatt érintett vagy bizonytalan komponens
```

### 3. Explicit hibatérkép bevezetése

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

### 4. RCA bekötése a `DiagnoseDashboardService` osztályba

A `DiagnoseDashboardService` konstruktorába bekerült a `RootCauseAnalyzer` függőség. A `GetDiagnosesAsync()` metódus a diagnózisok lekérése és a hibaállapotok frissítése után meghívja az új RCA-folyamatot:

```csharp
RunRootCauseAnalysis();
```

A `RunRootCauseAnalysis()` lépései:

1. korábbi gyökérhiba-jelölések törlése,
2. hierarchikus propagation és `CONSEQUENCE` állapotok beállítása,
3. gyökérhibák kiválasztása a mért hibák közül,
4. LED állapot beállítása aszerint, hogy van-e aktív gyökérhiba.

Ezzel a gyökérhiba-meghatározás a megjelenítési rétegből az üzleti/diagnosztikai logika rétegébe került.

### 5. Állapotfrissítés és hierarchikus terjesztés szétválasztása

A `TreeSearchF` és `TreeSearchW` metódusokat egyszerűsítettem. Korábban ezek nemcsak a megadott hibát állították `FAULT` vagy `WORKING` állapotba, hanem a `ParentId` és prioritás alapján gyermekhibák állapotát is módosították. Ez ugyan működőképes kaszkádot adott, de összemosta a ténylegesen mért hibát a következményhibával.

Az új működés:

```csharp
TreeSearchF("KommRfidUp") -> csak a KommRfidUp lesz FAULT
TreeSearchW("KommRfidUp") -> csak a KommRfidUp lesz WORKING
```

A diagnosztikai ciklus elején a service visszaállítja a hibákat `WORKING` állapotba, majd csak az aktuálisan mért vagy explicit módon ellenőrzött hibákat állítja `FAULT` állapotba. Ezután a `RootCauseAnalyzer.PropagateFaults()` külön lépésben állítja be a `CONSEQUENCE` állapotokat.

### 6. Dashboard megjelenítés bővítése

A `Dashboard.razor` már nem külön `if/else` blokkokkal jeleníti meg minden hiba állapotát, hanem hibalistákból rendereli a gombokat. Ez csökkenti az ismétlődő Razor kódot, és egyszerűbbé teszi az új állapotok megjelenítését.

A dashboardon megjelent az új következményállapot:

```text
Következmény = CONSEQUENCE
```

Ehhez új CSS osztály készült:

```css
.button_consequence
```

A jelmagyarázat is bővült, így a kezelő külön látja:

- elérhető,
- nem elérhető,
- hiba,
- következmény,
- gyökérhiba.

### 7. Dependency injection regisztráció

A `Startup.cs` fájlban regisztrálva lett az új osztály:

```csharp
services.AddScoped<RootCauseAnalyzer>();
```

Erre azért volt szükség, hogy a `DiagnoseDashboardService` konstruktoron keresztül megkapja az RCA modult.

## Új gyökérhiba- és propagation-szabály

Egy aktív hiba akkor lesz gyökérhiba, ha:

1. `FaultStatus == FAULT` vagy korábbi ciklusból `ROOTFAULT`,
2. nincs olyan aktív mért szülője az explicit hibatérkép szerint, amely szintén hibás,
3. ezért ő a legmagasabb szintű aktív mért hiba az adott hibaágban.

Egy hiba akkor lesz következmény, ha:

1. eredetileg `WORKING` vagy korábbi ciklusból `CONSEQUENCE`,
2. van aktív mért őse az explicit hibatérképben,
3. nem rendelkezik saját mért hibajellel.

Példák:

| Bemenet | Eredmény |
|---|---|
| `KommKozpont = FAULT` | `KommKozpont = ROOTFAULT`, alatta `KommKocsi`, `KommTartaly`, `KommRfidUp`, `GyarRfidOlv` stb. `CONSEQUENCE` |
| `KommRfidUp = FAULT` | `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = CONSEQUENCE` |
| `KommRfidUp = FAULT`, `GyarRfidOlv = FAULT` | `KommRfidUp = ROOTFAULT`, `GyarRfidOlv = FAULT` |
| `GyarRfidOlv = FAULT` | `GyarRfidOlv = ROOTFAULT` |
| `KommTartaly = FAULT`, `AramTartaly = FAULT` | `KommTartaly = ROOTFAULT`, `AramTartaly = FAULT` |

## Miért jobb ez a megoldás?

A módosítás után a rendszer nem pusztán prioritás alapján választ gyökérhibát, hanem a hibák közötti explicit ok-okozati kapcsolatokat is figyelembe veszi. A ténylegesen mért hibákat és a származtatott következményeket külön állapot reprezentálja, ezért a dashboard nem állítja tévesen, hogy minden érintett komponens saját hibát jelzett.

A fejlesztés további előnyei:

- a UI-ból kikerült a diagnosztikai döntési logika,
- a hibatérkép külön helyen módosítható,
- az adatbázisséma nem változott,
- az állapotfrissítés nem terjeszt automatikusan gyermekhibákat `FAULT` állapotként,
- a mért hiba és a következményhiba külön státuszt kapott,
- az RFID kommunikációs és RFID olvasási hiba viszonya tisztábban kezelhető,
- a dashboard jelmagyarázata bővült a következményhibákkal,
- az RCA később unit tesztekkel vagy részletesebb szabályokkal bővíthető.

## Korlátok és következő lépések

Ez a fejlesztési szakasz kódszintű hibatérképet használ, és nem módosítja az adatbázissémát. Ez szándékos, mert az első cél a logika tisztázása és a működés szakmailag védhető szétválasztása volt.

Következő lehetséges lépések:

1. unit tesztek írása a `RootCauseAnalyzer` osztályra,
2. külön következményhiba-panel vagy fa nézet bevezetése a dashboardon,
3. RFID állapotmodell bevezetése,
4. dashboardon külön gyökérhiba-összefoglaló panel kialakítása,
5. később valódi `ParentFaultId` adatmodell bevezetése adatbázis-migrációval.
