# FMS
Fault management system

![Architecktúra](https://user-images.githubusercontent.com/71183148/155537163-4fdb146e-98ce-4be3-a1d3-f378f6f70efb.PNG)

## Rest végpontok:

	FactorySimulation:

		-Demo power button: api/power

		-Pause: api/pause

		-Wake up: api/wakeUp

		-Kommunikációs hiba: api/communicationError

		-kommunikációs áramellátás: api/communicationPowerError

		-Szalag plc hiba: api/conveyorError

		-Sebesség állítás: api/speed

		-Kocsi: api/carError

		-LED: api/led

		-Tartály állapota: api/container

	DashBoard:

		-Dashboard oldal: api/dashboard

## IP-k:

	-BROKER: 192.168.0.100 (port:1883)
	-PC: 192.168.0.2 LAN, 
	-Kocsi Pi: 192.168.0.90
	-Üveg ESP: 192.168.0.91
	-Tank ESP: 192.168.0.51
	-RFID ESP: 192.168.0.52

## MQTT:

	-BROKER: TBD (port:1883)
	- FactoryServie
	- DashboadService
	- Tank ESP
	- Szállítmány ESP
	
## Hibalehetőségek:
		
	Gyártósor:
		- Szalag szenzorok
		- Tartály szenzorok
		- Targonca szenzorok
		- RFID olvasó
	Áramellátás:
		- Kommunikációs központ
		- Tartály
		- AVG Kocsi
		- Rendszer
	Kommunikáció:
		- Kommunikációs központ
		- Kommunikációs központ elérhető
		- Kocsi üvegek esp
		- Raktár RFID elérhető
		- Kommunikációs központ árammérő elérhető
		- Tartály árammérő elérhető
		- Tartály esp
		- Kocsi árammérő elérhető
		- Kocsi elérhető
		- Rendszer kommunikáció
