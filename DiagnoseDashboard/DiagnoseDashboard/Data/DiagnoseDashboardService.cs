using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DiagnoseDashboard.Model;
using Refit;

namespace DiagnoseDashboard.Data
{
    public class DiagnoseDashboardService
    {
        private static Diagnoses diagnoses = new Diagnoses();
        private static string carstate = "OFFLINE";
        public static string tankState = "OFFLINE";
        public static string bottleState = "OFFLINE";
        private static string baseUrl = "https://localhost:5004/";
        private IDashboardData dashboardData = RestService.For<IDashboardData>(baseUrl);
        private static bool connection;
        private static bool print;
        public ProfileMessages alert = ProfileMessages.NULL;
        public ProfileMessages answer = ProfileMessages.NULL;
        private FaultSearch faultSearch;
        List<FaultData> decrease = new List<FaultData>();
        List<FaultData> decreaseParentID = new List<FaultData>();

        public DiagnoseDashboardService(FaultSearch FaultSearch)
        {
            faultSearch = FaultSearch;
            decrease = faultSearch.faultDatas.OrderByDescending(x => x.Priority).ToList();
            decreaseParentID = faultSearch.faultDatas.OrderByDescending(x => x.Priority).ToList();
        }

        public async Task GetDiagnosesAsync()
        {
            diagnoses = await dashboardData.GetDiagnoses();
            await DiagnoseAnalyse();
        }

        public async Task TreeSearchW(string fault)
        {
            await Task.Run(async () =>
            {
                faultSearch.faultDatas.FirstOrDefault(item => item.Name == fault).FaultStatus = FaultStatus.WORKING;
                var diagnosesTemp = await dashboardData.GetDiagnoses();
                foreach (var getParentId in decrease)
                {
                    List<FaultData> child = new List<FaultData>();
                    foreach (var ownId in decreaseParentID)
                    {
                        if (ownId.ParentId == (int)getParentId.Priority)
                        {
                            child.Add(ownId);
                        }
                    }
                    foreach (var item in child)
                    {
                        if ((getParentId.FaultStatus == FaultStatus.WORKING) && child != null)
                        {
                            foreach (PropertyInfo propTemp in
                                typeof(Diagnoses)
                                .GetProperties()
                                .Where(p => p.PropertyType == typeof(DiagnoseData)))
                            {
                                DiagnoseData currentDiagnoseTemp = (DiagnoseData)propTemp.GetValue(diagnosesTemp, null);

                                foreach (PropertyInfo prop in
                                    typeof(Diagnoses)
                                    .GetProperties()
                                    .Where(p => p.PropertyType == typeof(DiagnoseData)))
                                {
                                    DiagnoseData currentDiagnose = (DiagnoseData)prop.GetValue(diagnoses, null);
                                    if (currentDiagnose.Name == item.Name && currentDiagnoseTemp.Name == item.Name && currentDiagnoseTemp.Data == false)
                                    {
                                        item.FaultStatus = FaultStatus.WORKING;
                                        currentDiagnose.Data = false;
                                        prop.SetValue(diagnoses, currentDiagnose);
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        public async Task TreeSearchF(string fault)
        {
            await Task.Run(() =>
            {
                faultSearch.faultDatas.FirstOrDefault(item => item.Name == fault).FaultStatus = FaultStatus.FAULT;
                Console.WriteLine(faultSearch.faultDatas.FirstOrDefault(item => item.Name == fault).Name + "ELEJÉN ÁTÁLÍTVA FAULTARA");
                foreach (var getParentId in decrease)
                {
                    List<FaultData> child = new List<FaultData>();
                    foreach (var ownId in decreaseParentID)
                    {
                        if (ownId.ParentId == (int)getParentId.Priority)
                        {
                            child.Add(ownId);
                        }
                    }
                    foreach (var item in child)
                    {
                        if ((getParentId.FaultStatus == FaultStatus.FAULT || getParentId.FaultStatus == FaultStatus.ROOTFAULT) && child != null)
                        {
                            item.FaultStatus = FaultStatus.FAULT;

                            foreach (PropertyInfo prop in
                                typeof(Diagnoses)
                                .GetProperties()
                                .Where(p => p.PropertyType == typeof(DiagnoseData)))
                            {
                                DiagnoseData currentDiagnose = (DiagnoseData)prop.GetValue(diagnoses, null);
                                if (currentDiagnose.Name == item.Name)
                                {
                                    currentDiagnose.Data = true;
                                    prop.SetValue(diagnoses, currentDiagnose);
                                }
                            }
                        }
                    }
                }
            });
        }

        public async Task DiagnoseAnalyse()
        {
            bool mqttIsConnected = await GetMqttStatus();

            // 1. SZINT: RENDSZER KOMMUNIKÁCIÓ ELLENŐRZÉSE
            // Ha ez hibás, minden más Fault lesz, és itt megállunk.
            if (diagnoses.KommRendszer.Data)
            {
                await TreeSearchF(FaultSearch.KommRendszer.Name);
                return; // Kilépünk, nem vizsgáljuk tovább
            }
            else
            {
                await TreeSearchW(FaultSearch.KommRendszer.Name);
            }

            // 2. SZINT: KOMMUNIKÁCIÓS KÖZPONT
            // Ha nincs MQTT vagy a központ hibát jelez
            if (!mqttIsConnected || diagnoses.KommKozpont.Data || diagnoses.KommKozpontUp.Data)
            {
                await TreeSearchF(FaultSearch.KommKozpont.Name);
                // TreeSearchF kaszkádolja a hibát az összes Level 3 (Eszköz) elemre, mivel azok szülője (29) ez.
                return; // Kilépünk
            }
            else
            {
                await TreeSearchW(FaultSearch.KommKozpont.Name);
                await TreeSearchW(FaultSearch.KommKozpontUp.Name);
            }

            // 3. SZINT: PÁRHUZAMOS ESZKÖZ ELLENŐRZÉSEK
            // Ide csak akkor jutunk, ha a Rendszer és a Központ rendben van.

            // --- A) KOCSI (CAR) ---
            string carstateTemp = await dashboardData.GetCarState();
            if (carstateTemp == "ONLINE")
            {
                carstate = carstateTemp;
                await TreeSearchW(FaultSearch.KommKocsi.Name);
            }
            else
            {
                carstate = "OFFLINE";
                await TreeSearchF(FaultSearch.KommKocsi.Name);
                // Ez automatikusan hibára teszi a gyerekeket (KocsiEsp, TargoncaSzenz)
            }

            // --- B) TARTÁLY (TANK) ---
            string tankStateTemp = await dashboardData.GetTankState();
            if (tankStateTemp == "ONLINE")
            {
                tankState = tankStateTemp;
                await TreeSearchW(FaultSearch.KommTartaly.Name);
                // Ha a tartály elérhető, megnézzük a specifikus hibáit (pl. áram)
                if (diagnoses.AramTartaly.Data) await TreeSearchF(FaultSearch.AramTartaly.Name); else await TreeSearchW(FaultSearch.AramTartaly.Name);
                if (diagnoses.GyarSzalagSzenz.Data) await TreeSearchF(FaultSearch.GyarSzalagSzenz.Name); else await TreeSearchW(FaultSearch.GyarSzalagSzenz.Name);
            }
            else
            {
                tankState = "OFFLINE";
                await TreeSearchF(FaultSearch.KommTartaly.Name);
                // Automatikusan Fault lesz: AramTartaly, GyarSzalagSzenz, stb.
            }

            // --- C) BOTTLE (KOCSI ÜVEGEK) ---
            // Ez a Kocsi alatt van elvileg, de külön kezeljük az állapotát
            string bottleStateTemp = await dashboardData.GetBottlesState();
            if (bottleStateTemp == "ONLINE")
            {
                bottleState = bottleStateTemp;
                await TreeSearchW(FaultSearch.KommKocsiEsp.Name);
            }
            else
            {
                bottleState = "OFFLINE";
                // Ha a Kocsi maga elérhető (ONLINE), de az ESP nem, akkor ez hiba.
                // Ha a Kocsi OFFLINE, akkor ez már úgyis Fault a fenti check miatt.
                await TreeSearchF(FaultSearch.KommKocsiEsp.Name);
            }

            // --- D) RFID ---
            // Két fő hibaforrás: Kommunikáció (Up) és Működés (Olv)

            // 1. Kommunikáció ellenőrzése
            if (diagnoses.KommRfidUp.Data)
            {
                await TreeSearchF(FaultSearch.KommRfidUp.Name);
            }
            else
            {
                await TreeSearchW(FaultSearch.KommRfidUp.Name);
            }

            // 2. Működési hiba (Rakomány, Áram, stb.)
            if (diagnoses.GyarRfidOlv.Data)
            {
                await TreeSearchF(FaultSearch.GyarRfidOlv.Name);
            }
            else
            {
                await TreeSearchW(FaultSearch.GyarRfidOlv.Name);
            }

            // --- EGYÉB HIBÁK AUTOMATIKUS FRISSÍTÉSE ---
            // Minden olyan hiba, amit manuálisan nem fedtünk le fentebb
            foreach (PropertyInfo prop in
                    typeof(Diagnoses)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(DiagnoseData)))
            {
                DiagnoseData currentDiagnose = (DiagnoseData)prop.GetValue(diagnoses, null);

                // Kihagyjuk azokat, amiket már manuálisan kezeltünk a struktúrában, 
                // nehogy felülírjuk a hierarchikus logikát.
                if (currentDiagnose.Name == FaultSearch.KommRendszer.Name ||
                    currentDiagnose.Name == FaultSearch.KommKozpont.Name ||
                    currentDiagnose.Name == FaultSearch.KommKocsi.Name ||
                    currentDiagnose.Name == FaultSearch.KommTartaly.Name ||
                    currentDiagnose.Name == FaultSearch.KommRfidUp.Name ||
                    currentDiagnose.Name == FaultSearch.GyarRfidOlv.Name)
                {
                    continue;
                }

                if (currentDiagnose.Data)
                {
                    await TreeSearchF(currentDiagnose.Name);
                }
                else
                {
                    // Csak akkor állítjuk vissza Workingre, ha a szülője nem Fault!
                    // Ezt a TreeSearchW elvileg ellenőrzi (getParentId check), de biztos ami biztos.
                    await TreeSearchW(currentDiagnose.Name);
                }
            }
        }

        public async Task<bool> GetMqttStatus()
        {
            var status = await dashboardData.GetMqttStatus();
            Console.WriteLine("KOMM STATUS: " + status);
            if (status)
            {
                faultSearch.faultDatas.FirstOrDefault(item => item.Name == FaultSearch.KommKozpont.Name).FaultStatus = FaultStatus.WORKING;
            }
            else if (status == false)
            {
                faultSearch.faultDatas.FirstOrDefault(item => item.Name == FaultSearch.KommKozpont.Name).FaultStatus = FaultStatus.FAULT;
            }
            return status;
        }
        public async Task MQTTConnectionAsync()
        {
            await dashboardData.ConnectMQTT();
        }

        public async Task<bool> MQTTConnectionLostAsync()
        {
            connection = await dashboardData.GetLostConnection();
            Console.WriteLine("Connection: " + connection);
            return connection;
        }

        public async Task ConnectMQTT()
        {
            print = await MQTTConnectionLostAsync();
            if (print == false)
            {
                try
                {
                    await MQTTConnectionAsync();
                }
                catch (Exception)
                {
                    Console.WriteLine("Nem lehetett csatlakozni");
                    answer = ProfileMessages.MQTTNOTUP;
                    Console.WriteLine("ConnectMQTT No: " + answer);
                    faultSearch.faultDatas.FirstOrDefault(item => item.Name == FaultSearch.KommKozpont.Name).FaultStatus = FaultStatus.FAULT;
                }
            }
            else
            {
                answer = ProfileMessages.MQTTISREACHABLE;
                Console.WriteLine("ConnectMQTT Yes: " + answer);
                try
                {
                    await MQTTConnectionAsync();
                    faultSearch.faultDatas.FirstOrDefault(item => item.Name == FaultSearch.KommKozpont.Name).FaultStatus = FaultStatus.WORKING;
                }
                catch (Exception)
                {
                }
                
            }
            CheckConnection();
        }

        public void CheckConnection()
        {
            if (answer == ProfileMessages.MQTTNOTUP)
            {
                alert = ProfileMessages.MQTTNOTUP;
            }
            else if (answer == ProfileMessages.MQTTISREACHABLE)
            {
                alert = ProfileMessages.MQTTISREACHABLE;
            }
        }

        public async Task LedFailure()
        {
            await dashboardData.LEDRed(FaultSearch.Led);
        }
    }
}
