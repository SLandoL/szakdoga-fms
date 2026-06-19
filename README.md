# FMS – Fault Management System

Ipari hibamenedzsment-demonstráció gyártósor-szimulációval, MQTT-kommunikációval, fizikai eszközökkel és gyökérhiba-elemző dashboarddal.

![Architektúra](https://user-images.githubusercontent.com/71183148/155537163-4fdb146e-98ce-4be3-a1d3-f378f6f70efb.PNG)

## Demonstrátori útmutató

Ez a README azoknak szól, akik a rendszert később látogatóknak vagy hallgatóknak mutatják be. A hangsúly a demó előkészítésén, használatán, bemutatási sorrendjén, elérési adatain és gyors hibaelhárításán van.

A részletes szakdolgozati/fejlesztési dokumentáció a README végén linkelve van.

---

## 1. Mit érdemes bemutatni?

A demó fő üzenete:

1. a fizikai eszközök és a webalkalmazások MQTT-n keresztül együttműködnek;
2. a Control Panelről normál működési parancsok és szimulált hibák adhatók ki;
3. a diagnosztikai dashboard elkülöníti a közvetlenül mért hibát, a következményhibát és a feltételezett gyökérhibát;
4. az RFID-panel külön mutatja az ESP, a két olvasó és a rakományegyezés állapotát;
5. a kocsi MQTT-kezelése a legutóbbi merge után stabilabb: védett callback, biztonságos inicializálás, kontrollált heartbeat és robusztusabb parancsfeldolgozás van benne;
6. a ResetPos debug kapcsoló élvezérelt eseményként működik: bármelyik kapcsolóél után a következő megálló a gyár lesz;
7. magasabb szintű kommunikációs hiba esetén az alsóbb komponensek következményként, nem automatikusan önálló hibaként jelennek meg.

---

## 2. Fő komponensek

| Komponens | Szerepe a demóban |
| --- | --- |
| **FactorySimulation** | Control Panel, Digital Factory, sebesség- és LED-beállítás, valamint hibák szimulálása. |
| **FactoryService** | A kezelőfelület parancsainak feldolgozása és MQTT-üzenetek publikálása. |
| **DiagnoseService** | MQTT-diagnózisok, heartbeat adatok és RFID-állapot feldolgozása. |
| **DiagnoseDashboard** | Mért hibák, következmények, gyökérhibák és RFID-állapot megjelenítése. |
| **MQTT broker** | Üzenetközvetítés a komponensek között. |
| **Kocsi Raspberry Pi / PiCar** | Vonalkövető kocsi, útvonalállapot, StopLeft/StopRight/ResetPos kezelés és heartbeat. |
| **Tank ESP** | Tartály- és LED-funkciók, kapcsolótábla olvasása, heartbeat. |
| **Bottle ESP** | Kocsi rakományát reprezentáló LED-ek és heartbeat. |
| **RFID ESP** | Tank- és raktároldali RFID-olvasók, rakományazonosítók és heartbeat. |

---

## 3. Hozzáférések, jelszavak és elérési adatok

> A rendszer elkülönített demonstrációs környezetben fut. A fejlesztés végén a repó privátra lesz állítva. A jelszavakat ettől függetlenül ne használd más rendszerben.

| Erőforrás | Elérés / felhasználó | Jelszó / megjegyzés |
| --- | --- | --- |
| FMS Wi-Fi | SSID: `FMS-WiFi` | `I40okos%` |
| Wi-Fi konfiguráció | Felhasználó: `Administrator` | `kiskacsa` |
| Demo PC | LAN IP: `192.168.0.2` | PC-jelszó: `kiskacsa` |
| MQTT broker | `192.168.0.100:1883` | Nincs dokumentált MQTT-felhasználó vagy MQTT-jelszó |
| Kocsi Raspberry Pi | `ssh pi@192.168.0.90` | Az SSH-jelszó nincs dokumentálva a repóban; ne feltételezz alapértelmezett jelszót |
| ESP-k | USB-s soros kapcsolat, Arduino IDE / Serial Monitor | Soros sebesség jellemzően `115200 baud` |

### Webes felületek

| Felület | Demo PC-n | Másik gépről az FMS hálózaton |
| --- | --- | --- |
| FactorySimulation | `http://localhost:5000` | `http://192.168.0.2:5000` |
| Control Panel | `http://localhost:5000/ControlPanel` | `http://192.168.0.2:5000/ControlPanel` |
| Digital Factory | `http://localhost:5000/DigitalFactory` | `http://192.168.0.2:5000/DigitalFactory` |
| Diagnosztikai dashboard | `http://localhost:5007/dashboard` | `http://192.168.0.2:5007/dashboard` |
| FactoryService Swagger | `http://localhost:5003/swagger` | `http://192.168.0.2:5003/swagger` |
| DiagnoseService Swagger | `http://localhost:5005/swagger` | `http://192.168.0.2:5005/swagger` |

HTTPS portok:

- FactorySimulation: `https://localhost:5001`
- FactoryService: `https://localhost:5002/swagger`
- DiagnoseService: `https://localhost:5004/swagger`
- DiagnoseDashboard: `https://localhost:5006/dashboard`

### Eszközök IP-címei

| Eszköz | Cím |
| --- | --- |
| MQTT broker | `192.168.0.100` |
| Demo PC LAN | `192.168.0.2` |
| Kocsi Raspberry Pi | `192.168.0.90` |
| Bottle ESP | tervezett cím: `192.168.0.91` |
| Tank ESP | `192.168.0.51` |
| RFID ESP | tervezett cím: `192.168.0.52`; a jelenlegi RFID firmware DHCP-t használ |

---

## 4. Gyors indítás

### Fizikai előkészítés

1. Tedd szabaddá a kocsi pályáját, és ellenőrizd a kábeleket.
2. Indítsd el az MQTT brokert.
3. Kapcsold be a Tank ESP-t, Bottle ESP-t, RFID ESP-t és a kocsi Raspberry Pi-jét.
4. Ellenőrizd, hogy a demo PC eléri a `192.168.0.100` címet.
5. Készítsd elő az RFID-kártyákat. Az ajánlott adattartalom: `XXX<rakományazonosító>XXX`.
6. Ellenőrizd, hogy a StopLeft, StopRight és ResetPos kapcsolók alapállásban vannak-e.

### Szoftverek indítása

Nyisd meg Visual Studio 2022-ben:

```text
FMS/FMS.sln
```

A demóhoz szükséges projektek:

```text
FactoryService
DiagnoseService
FactorySimulation
DiagnoseDashboard
```

Ajánlott kézi indítási sorrend:

1. `FactoryService`
2. `DiagnoseService`
3. `FactorySimulation`
4. `DiagnoseDashboard`

Visual Studio-ban egyszerre induló projektként is beállíthatók:

```text
Solution → Properties → Multiple startup projects
```

Mind a négy projektnél válaszd a `Start` műveletet.

### Kocsi vezérlőkód indítása

A frissített, bemutatóhoz használandó kocsioldali fájl:

```text
Kocsi/example/car.py
```

Fontos: a gyökérkönyvtárban lévő régi `car.py` nem az új, stabilizált változat. A demóban a `Kocsi/example/car.py` alapján futó kódot használd.

Indításkor a kocsi konzolján ezt érdemes ellenőrizni:

```text
PiCar hardware initialized
Connected to MQTT broker: 192.168.0.100
```

A hardverobjektumok inicializálása az MQTT kapcsolat előtt történik, így egy korán érkező MQTT-üzenet nem tud még nem inicializált `fw`, `bw` vagy `lf` objektumra hivatkozni.

### MQTT-kapcsolat

1. Nyisd meg a FactorySimulation felületet.
2. Jelentkezz be, ha szükséges.
3. Válaszd a bal oldali **Reconnect to MQTT** menüpontot.
4. Várd meg az **„Elérhető az MQTT Broker!”** visszajelzést.
5. Nyisd meg külön böngészőfülön a Control Panelt, a Digital Factory nézetet és a dashboardot.

### Bemutatás előtti ellenőrzőlista

- `Pause` kikapcsolva;
- minden hibagomb alapállapotban;
- kocsisebesség kezdetben 30–50%;
- MQTT-kapcsolat aktív;
- a dashboardon nincs gyökérhiba;
- kocsi, Tank ESP és Bottle ESP heartbeat friss;
- RFID ESP online, mindkét olvasó friss;
- az RFID-kártyák olvashatók;
- StopLeft, StopRight és ResetPos nem ad folyamatosan ismétlődő parancsot;
- minden szükséges böngészőfül meg van nyitva.

A hibagombok állapota adatbázisban megmaradhat, ezért egy előző bemutató után aktív állapotban maradt hibákat kézzel vissza kell állítani.

---

## 5. Control Panel használata

- **Pause:** a kocsi szüneteltetése és továbbengedése.
- **Wake Up:** pillanatnyi ébresztőparancs; a gomb rövid idő után visszaáll.
- **Kocsi sebessége:** csúszkával vagy nyilakkal állítható.
- **LED színbeállítás:** piros, zöld, kék vagy Beer animáció.
- **Hibagombok:** első kattintás aktiválja, második kattintás megszünteti a hibát.

A **Rendszer** gomb rendszerkommunikációs hibát generál, nem egyszerű főkapcsoló.

Aktív hiba esetén a kocsi automatikusan megállhat. A hiba megszüntetése után ellenőrizd az összes hibagombot és a Pause állapotát, majd szükség esetén használd a Wake Up parancsot.

Két kattintás között várj legalább 1–2 másodpercet.

---

## 6. Dashboard jelmagyarázat

| Megjelenés | Jelentés |
| --- | --- |
| Zöld – **Elérhető** | Nincs aktív hiba. |
| Sárga – **Mért hiba** | A komponens saját hibajele aktív. |
| Kék, szaggatott – **Következmény** | Egy felsőbb hiba miatt az állapot bizonytalan vagy érintett, de nincs saját megbízható hibajel. |
| Piros, háromszög – **Gyökérhiba** | Az adott hibaág legmagasabb szintű feltételezett oka. |

A következményállapot nem jelenti azt, hogy a komponens biztosan meghibásodott. Központi kommunikációs hiba esetén az alsóbb eszközökről egyszerűen nem áll rendelkezésre megbízható állapotadat.

### Heartbeat-időzítések

- Kocsi: az új kódban legfeljebb másodpercenként egyszer publikál `MQTTState -> ONLINE` üzenetet.
- Tank ESP: körülbelül 500 ms-onként küld `ONLINE` állapotot a `tankesp` topicra.
- Kocsi, Tank ESP és Bottle ESP backend oldalon: körülbelül `15 másodperc` után offline.
- RFID ESP és RFID reader státuszok: körülbelül `6 másodperc` után elavult vagy offline.
- RFID firmware publikálási periódusa: körülbelül `2 másodperc`.

Eszközlekapcsolás bemutatásakor várd meg a megfelelő timeoutot.

---

## 7. Kapcsolótábla és debug kapcsoló

A Tank ESP olvassa a kapcsolótábla bemeneteit, a kocsi pedig MQTT-n kapja meg az eseményeket.

### Aktuális pin-kiosztás

| Funkció | ESP8266 GPIO | NodeMCU jelölés | MQTT topic |
| --- | --- | --- | --- |
| StopLeft kapcsoló | GPIO0 | D3 | `StopRight` |
| StopRight kapcsoló | GPIO2 | D4 | `StopLeft` |
| ResetPos debug kapcsoló | GPIO14 | D5 | `ResetPos` |

Fontos: a StopRight korábban hibásan D6/GPIO12-re volt konfigurálva. A jelenlegi javított kódban StopRight = D4/GPIO2. A ResetPos továbbra is D5/GPIO14.

### ResetPos működése

A ResetPos nem diagnosztikai hiba és nem tartós útvonalállapot. Bármelyik kapcsolóél ugyanazt jelenti:

```text
ResetPos kapcsoló átvált
→ egyszeri debug esemény
→ a kocsi következő megállója a gyár legyen
```

Elvárt konzolüzenet a kocsin:

```text
Debug switch event: next stop forced to factory
```

A ResetPos használata nem hoz létre új dashboard hibát, nem jelenik meg új FaultData elemként, és nem változtatja meg az RCA vagy RFID logikát.

---

## 8. Kocsi MQTT parancsok

| Parancs | Jelentés |
| --- | --- |
| `carSpeed,<0-100>` | Kocsisebesség beállítása százalékban. |
| `Paused,True` | Kocsi megállítása. |
| `Paused,False` | Manuális pause feloldása. |
| `WakeUp,True` | Ébresztés / pause feloldási kísérlet. |
| `carLedColor,<szín>` | LED-szín módosítása. |
| `CarGOTank` | A tank oldali állomás kész. |
| `CarGOBottle` | A bottle/rakomány oldal kész. |
| `CarGOContainer` | A konténeroldali állomás kész. |
| `ForceNextStopFactory` | Szoftveres debug parancs: következő megálló gyár. |

A `CarGOTank` és `CarGOBottle` után a kocsi csak akkor indul tovább, ha mindkét kész jel megérkezett. Ezt a konzolon a következő jellegű üzenetek mutatják:

```text
CarGOTank received
CarGOBottle received
Flags: tank = True bottle = True container = True
Both stations ready, restarting car
Publishing car-esp start, rc = ...
```

Ha a kocsi az első megálló után nem indul tovább, először ezeket a topicokat és konzolüzeneteket ellenőrizd.

---

## 9. Ajánlott bemutatási forgatókönyv

1. **Normál állapot:** Control Panel, mozgó kocsi, Digital Factory és zöld dashboard.
2. **Egyszerű hiba:** például szalagszenzor hiba, majd gyökérhiba-kijelölés és kocsi megállása.
3. **Visszaállítás:** ugyanazt a hibát kapcsold ki, várj 1–2 másodpercet, szükség esetén Wake Up.
4. **Magasabb szintű hiba:** Rendszer hiba, majd gyökérhiba és következményállapotok bemutatása.
5. **RFID-egyezés:** azonos kártyákkal két azonos rakományazonosító.
6. **RFID-eltérés:** egyik kártya cseréje, rakományhiba bemutatása.
7. **RFID-kiesés:** RFID ESP vagy reader kikapcsolása, majd legalább 6 másodperc várakozás.
8. **ResetPos debug:** ResetPos átváltása, következő megálló gyár, diagnosztikai hiba nélkül.
9. **Lezárás:** minden hiba visszaállítása és normál dashboard ellenőrzése.

---

## 10. RFID-kártyák

Ajánlott formátum:

```text
XXX<cargoId>XXX
```

Az `XXX` kezdő- és végmarker segít a hasznos rakományazonosító kiemelésében. Az ékezetes karakterek UTF-8 kódolása több bájtot használhat, ezért rövid azonosítókat használj.

A bemutatóhoz legyen kéznél két azonos tartalmú kártya és legalább egy eltérő tartalmú kártya.

---

## 11. Gyors hibaelhárítás

### A Control Panel nem működik vagy a gombok le vannak tiltva

1. Ellenőrizd a `FactoryService` futását.
2. Frissítsd az oldalt és várd meg az adatbetöltést.
3. Ellenőrizd a Rendszer és Pause állapotot.
4. Használd a **Reconnect to MQTT** menüpontot.
5. Ellenőrizd a FactoryService Swagger oldalt.

### A dashboard nem frissül

1. Ellenőrizd a `DiagnoseService` és `DiagnoseDashboard` futását.
2. Nyisd meg a DiagnoseService Swagger oldalt.
3. Ellenőrizd az MQTT brokert.
4. Várj legalább egy frissítési ciklust.
5. RFID esetén várd meg a 6 másodperces timeoutot és az új heartbeatet.

### A kocsi nem indul el

1. Kapcsolj ki minden aktív hibát és a Pause állapotot.
2. Állíts be 30–50% sebességet.
3. Nyomd meg a Wake Up gombot.
4. MQTT Explorerrel ellenőrizd a `carManagement` topicot.
5. A kocsi konzolján ellenőrizd, hogy nincs-e `MQTT callback error` vagy `Fatal car controller error`.

### A kocsi megáll az első állomás után és nem indul tovább

1. MQTT Explorerrel figyeld a `carManagement` topicot.
2. Ellenőrizd, hogy megérkezik-e a `CarGOTank` és a `CarGOBottle` parancs.
3. A kocsi konzolján keresd a `Flags:` és `Both stations ready, restarting car` üzeneteket.
4. Ha csak az egyik kész jel érkezik meg, akkor a kocsi helyesen várakozik a másik állomásra.
5. Ellenőrizd, hogy nincs-e aktív Pause vagy szimulált hiba.

### A ResetPos nem úgy viselkedik, ahogy vártad

1. A ResetPos D5/GPIO14-en van.
2. Mindkét kapcsolóél ugyanazt jelenti: következő megálló gyár.
3. A kapcsoló tartós állása nem állít folyamatosan route state-et.
4. A kocsi konzolján keresd a `Debug switch event: next stop forced to factory` üzenetet.
5. A dashboardon emiatt nem szabad új diagnosztikai hibának megjelennie.

### StopRight nem működik

1. Ellenőrizd, hogy a Tank ESP-re a legfrissebb `tankesp/tankesp.ino` van-e feltöltve.
2. A StopRight helyes pinje D4/GPIO2.
3. Ne a régi D6/GPIO12 kiosztást keresd.
4. MQTT Explorerben figyeld, hogy kapcsoláskor érkezik-e üzenet a megfelelő topicra.

### Minden eszköz offline

1. Ellenőrizd az MQTT brokert.
2. Ellenőrizd, hogy minden eszköz ugyanazon az FMS hálózaton van-e.
3. Ellenőrizd a DiagnoseService MQTT-kapcsolatát.
4. Figyeld MQTT Explorerrel a heartbeat topicokat.

### RFID ESP online, de nincs rakományadat

1. Ellenőrizd a két reader friss és működő állapotát.
2. Helyezd stabilan a kártyát az olvasóhoz.
3. Ellenőrizd az `XXX...XXX` markert.
4. Figyeld az „Utolsó olvasás” időpontját; sikertelen olvasás nem írja felül az utolsó sikeres cargo ID-t.

---

## 12. Fontos konfigurációs megjegyzések

- A demóhoz használandó kocsioldali fájl: `Kocsi/example/car.py`.
- A gyökérkönyvtárban lévő régi `car.py` nem a legfrissebb, stabilizált kocsi vezérlőkód.
- A `Kocsi/example/car.py` már az `192.168.0.100:1883` broker címet használja.
- Az `espbottles/espbottles.ino` régi `172.22.x.x` beállításokat tartalmazhat.
- A gyökérkönyvtár `espbottles.ino` fájljában ellenőrizni kell a helyi IP-t, mert nem ütközhet a Tank ESP címével.
- Az RFID firmware DHCP-t használ; a fix `192.168.0.52` címhez DHCP-foglalás vagy statikus konfiguráció szükséges.
- A Tank ESP kapcsolótábla aktuális kiosztása: StopLeft D3/GPIO0, StopRight D4/GPIO2, ResetPos D5/GPIO14.

Feltöltés előtt ellenőrizd:

```text
Wi-Fi SSID
Wi-Fi jelszó
MQTT broker címe
statikus IP
gateway
subnet
MQTT topicok
kapcsolók pin-kiosztása
```

---

## 13. Fő MQTT topicok

| Topic | Funkció |
| --- | --- |
| `Diagnoses` | Diagnosztikai jelek. |
| `carManagement` | Kocsivezérlés. |
| `CarLocation` | Digital Factory kocsihelyzet. |
| `tank-esp` | Tank ESP vezérlése. |
| `car-esp` | Bottle ESP vezérlése. |
| `MQTTState` | Kocsi heartbeat. |
| `tankesp` | Tank ESP heartbeat. |
| `caresp` | Bottle ESP heartbeat. |
| `StopLeft` | Extra megálló kapcsoló MQTT-je. |
| `StopRight` | Extra megálló kapcsoló MQTT-je. |
| `ResetPos` | Debug kapcsoló: következő megálló gyár. |
| `RFID/Heartbeat` | RFID ESP életjel. |
| `RFID/TankReader/Status` | Tank oldali olvasóállapot. |
| `RFID/WarehouseReader/Status` | Raktároldali olvasóállapot. |
| `RFID/TankReader/Cargo` | Tank oldali rakomány. |
| `RFID/WarehouseReader/Cargo` | Raktároldali rakomány. |
| `RFID/CargoMatch` | Rakományegyezés. |

---

## 14. REST és Swagger referencia

Fő FactoryService útvonalak:

```text
api/Factory/ControlPanel/CarSpeed
api/Factory/ControlPanel/System
api/Factory/ControlPanel/Pause
api/Factory/ControlPanel/WakeUp
api/Factory/ControlPanel/PLCFailure
api/Factory/ControlPanel/CarError
api/Factory/ControlPanel/ContainerEmpty
api/Factory/ControlPanel/RFID
api/Factory/ControlPanel/LED
api/Factory/ControlPanel/RendszerElectro
api/Factory/ControlPanel/TartalyElectro
api/Factory/ControlPanel/KocsiElectro
api/Factory/ControlPanel/KommKozElectroKomm
api/Factory/ControlPanel/TartalyElectroKomm
api/Factory/ControlPanel/KocsiElectroKomm
api/Factory/DigitalFactory/CarLocation
```

A teljes és aktuális végpontlista mindig a Swagger felületen ellenőrizendő.

---

## 15. Bemutató lezárása

1. Kapcsold ki az összes szimulált hibát.
2. Kapcsold ki a `Pause` állapotot.
3. Állítsd meg biztonságosan a kocsit.
4. Ellenőrizd, hogy a dashboard visszaállt-e normál állapotba.
5. Zárd be a webalkalmazásokat és állítsd le a négy .NET projektet.
6. Kapcsold le a fizikai eszközöket a helyi laborrend szerint.
7. A következő demonstrátornak jelezd, ha egy eszköz, RFID-kártya, kapcsoló vagy hozzáférés nem működött megfelelően.

---

## 16. További dokumentáció

- [Gyökérhiba-elemzés fejlesztési napló](docs/development-log-root-cause-analyzer.md)
- [Gyökérhiba-elemzés tesztterv](docs/root-cause-analyzer-test-plan.md)
- [DiagnoseDashboardService fejlesztési napló](docs/development-log-diagnose-dashboard-service.md)
- [RFID diagnosztika és heartbeat fejlesztési napló](docs/development-log-rfid-diagnostics-heartbeat.md)
- [RFID diagnosztika tesztterv](docs/rfid-diagnostics-test-plan.md)
- [Debug kapcsoló fejlesztési napló](docs/development-log-debug-switch-edge-route.md)
- [Debug kapcsoló tesztterv](docs/debug-switch-edge-route-test-plan.md)
- [Kocsi / SunFounder PiCar-S README](Kocsi/README.md)
- `FMS-pas.txt` – meglévő labor-hozzáférési jegyzet

A fejlesztési naplók a rendszer belső működését és a szakdolgozati fejlesztéseket részletezik. A teszttervek kézi MQTT-példákat és ellenőrizhető hibaeseteket tartalmaznak.
