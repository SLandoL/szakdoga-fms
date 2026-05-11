# Fejlesztési napló - 2. szakasz: DiagnoseDashboardService egyszerűsítése

## Cél

A második fejlesztési szakasz célja a `DiagnoseDashboardService` felelősségeinek tisztítása volt. Az előző szakaszban a gyökérhiba-meghatározás már külön `RootCauseAnalyzer` osztályba került, és a dashboard közvetlenül nem hívja a régi prioritásalapú RCA metódusokat. Ebben a lépésben a service belső diagnosztikai folyamata lett olvashatóbb és jobban tagolt.

A cél az volt, hogy a `DiagnoseDashboardService` ne egyetlen nagy metódusban keverje a diagnózislekérést, az MQTT státusz értelmezését, az eszközállapotok vizsgálatát, az RFID logikát és a maradék diagnózisok feldolgozását, hanem koordinátorként működjön.

## Kiindulási állapot

A `GetDiagnosesAsync()` már az előző fejlesztési szakasz után is a megfelelő irányba mutatott:

```csharp
diagnoses = await dashboardData.GetDiagnoses();
await DiagnoseAnalyse();
RunRootCauseAnalysis();
```

Ez azt jelenti, hogy a dashboard a `DashboardViewModel`-en keresztül kéri le a diagnózisokat, a service frissíti a mért hibaállapotokat, majd a `RootCauseAnalyzer` futtatja a következményterjesztést és a gyökérhiba-kiválasztást.

A probléma az volt, hogy a `DiagnoseAnalyse()` metódus még mindig túl sok részfeladatot tartalmazott egyben:

- rendszerkommunikáció ellenőrzése,
- MQTT/központi kommunikáció ellenőrzése,
- kocsi állapotának lekérése,
- bottle ESP állapotának lekérése,
- tartály állapotának lekérése,
- RFID diagnózisok kezelése,
- maradék diagnosztikai jelek reflektív feldolgozása.

## Elvégzett módosítások

### 1. A `DiagnoseAnalyse()` koordinátorrá alakítása

A `DiagnoseAnalyse()` most már csak a diagnosztikai ciklus fő lépéseit hívja meg:

```csharp
bool mqttIsConnected = await GetMqttStatus();
ResetFaultStatuses();

if (await AnalyseSystemCommunication()) return;
if (await AnalyseMqttCenter(mqttIsConnected)) return;

bool carOnline = await AnalyseCar();
bool tankOnline = await AnalyseTank();

await AnalyseRfid();
await AnalyseRemainingDiagnoses(carOnline, tankOnline);
```

Ezzel a metódus szerepe világosabb lett: nem részletszabályokat tartalmaz, hanem a teljes diagnosztikai folyamat sorrendjét írja le.

### 2. Rendszerkommunikáció külön metódusban

Új metódus:

```csharp
private async Task<bool> AnalyseSystemCommunication()
```

Ez a legfelső szintű rendszerkommunikációs hibát kezeli. Ha a `KommRendszer` diagnosztikai jel aktív, akkor a service beállítja a megfelelő `FaultData` elemet `FAULT` állapotra, és a diagnosztikai ciklus ezen a ponton megáll. Ez megőrzi azt a korábbi működést, hogy magasabb szintű kommunikációs hiba esetén az alsóbb szintek nem kerülnek külön mért hibaként értékelésre.

### 3. MQTT és központi kommunikáció külön metódusban

Új metódus:

```csharp
private async Task<bool> AnalyseMqttCenter(bool mqttIsConnected)
```

Ez kezeli az MQTT kapcsolat és a központi kommunikáció állapotát. Ha az MQTT nem elérhető, vagy a `KommKozpont` diagnosztikai jel aktív, akkor a `KommKozpont` lesz mért hiba. Ha ezek rendben vannak, de a `KommKozpontUp` aktív, akkor az önálló diagnosztikai jelként jelenik meg.

A metódus logikai visszatérési értéke azt jelzi, hogy a diagnosztikai ciklusnak meg kell-e állnia ezen a magasabb szinten.

### 4. Kocsiág külön metódusban

Új metódus:

```csharp
private async Task<bool> AnalyseCar()
```

Ez lekéri a kocsi állapotát, normalizáltan ellenőrzi az `ONLINE` értéket, majd csak online kocsi mellett vizsgálja tovább a kocsihoz tartozó bottle ESP állapotát.

A metódus visszaadja, hogy a kocsi online volt-e. Erre a maradék diagnózisok feldolgozásánál van szükség, mert offline szülő mellett a kocsi alatti gyermekhibákat nem szabad saját mért hibaként értékelni.

### 5. Bottle ESP külön metódusban

Új metódus:

```csharp
private async Task AnalyseBottle()
```

Ez a bottle ESP állapotát kezeli. A metódus csak akkor fut, ha a kocsi online. Így megmarad az előző szakaszban kialakított szülő-gyermek gate-elés: ha a kocsi offline, akkor a bottle ESP állapota nem tekinthető megbízható saját mérésnek.

### 6. Tartályág külön metódusban

Új metódus:

```csharp
private async Task<bool> AnalyseTank()
```

Ez lekéri a tartály állapotát, majd online tartály mellett kezeli a tartály alatti konkrét hibákat, például az `AramTartaly` és `GyarSzalagSzenz` jeleket. Offline tartálynál csak a `KommTartaly` lesz mért hiba, az alatta lévő hibák következményként jelennek meg a `RootCauseAnalyzer` alapján.

### 7. RFID logika külön metódusban

Új metódus:

```csharp
private async Task AnalyseRfid()
```

Ez kezeli az RFID kommunikációs és RFID olvasási/rakományegyezési hibák elkülönítését. A működés nem változott az előző szakaszhoz képest:

- ha `KommRfidUp` aktív, akkor ez lesz mért hiba,
- ha `KommRfidUp` nem aktív, de `GyarRfidOlv` aktív, akkor az RFID olvasási/rakományegyezési hiba lesz mért hiba,
- ha egyik sem aktív, mindkét állapot working marad.

### 8. Maradék diagnózisok külön metódusban

Új metódus:

```csharp
private async Task AnalyseRemainingDiagnoses(bool carOnline, bool tankOnline)
```

Ez tartalmazza a korábbi reflektív feldolgozást. A metódus végigmegy a `Diagnoses` osztály `DiagnoseData` típusú property-jein, és azokat a hibákat állítja `FAULT` állapotba, amelyeket nem kezeltek korábban explicit módon.

A `ShouldSkipGenericDiagnose(...)` továbbra is megakadályozza, hogy a már kezelt magasabb szintű hibák, illetve offline szülő alatti gyermekhibák újra mért hibaként kerüljenek feldolgozásra.

## Miért jobb ez a megoldás?

A működés szándékosan nem változott jelentősen, viszont a service belső szerkezete áttekinthetőbb lett. A diagnosztikai ciklusban most jól láthatók a fő döntési pontok:

1. felső szintű rendszerhiba,
2. központi kommunikációs hiba,
3. eszközágak állapota,
4. RFID diagnózis,
5. maradék diagnosztikai jelek.

Ez szakdolgozati szempontból azért fontos, mert a service így már nem egy nagy, nehezen magyarázható metódusként jelenik meg, hanem koordinátorként, amely külön diagnosztikai részfeladatokat hív meg. A gyökérhiba-meghatározás továbbra is külön modulban marad, ezért a megjelenítési réteg, a service koordinációs logikája és az RCA algoritmus felelőssége jobban elkülönül.

## Fontos szakdolgozati megfogalmazás

A továbbfejlesztés során a `DiagnoseDashboardService` belső diagnosztikai folyamata kisebb, célzott metódusokra lett bontva. A service ezzel koordinátorszerepbe került: lekéri a diagnosztikai és eszközállapot-adatokat, frissíti a mért `FaultData` állapotokat, majd meghívja a külön `RootCauseAnalyzer` modult. A felhasználói felület továbbra sem végez gyökérhiba-számítást, hanem csak a service által előállított állapotot jeleníti meg.

## Korlátok és következő lépések

Ez a szakasz elsősorban szerkezeti refaktor volt. Nem történt adatbázis-migráció, és a diagnosztikai szabályok lényegi működése sem lett újratervezve. A következő logikus fejlesztések:

1. a `TreeSearchF` és `TreeSearchW` metódusok későbbi átnevezése egyértelműbb `SetFaultStatus` jellegű segédfüggvényre,
2. a tank alatti diagnózisok teljes körű explicit kezelése,
3. service-szintű unit vagy integrációs tesztek írása,
4. a dashboard gyökérhiba-összefoglaló panelének kialakítása.
