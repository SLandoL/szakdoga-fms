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
        private readonly RootCauseAnalyzer rootCauseAnalyzer;

        public DiagnoseDashboardService(FaultSearch FaultSearch, RootCauseAnalyzer RootCauseAnalyzer)
        {
            faultSearch = FaultSearch;
            rootCauseAnalyzer = RootCauseAnalyzer;
        }

        public async Task GetDiagnosesAsync()
        {
            diagnoses = await dashboardData.GetDiagnoses();
            await DiagnoseAnalyse();
            RunRootCauseAnalysis();
        }

        private void RunRootCauseAnalysis()
        {
            rootCauseAnalyzer.ResetRootFaults(faultSearch.faultDatas);
            rootCauseAnalyzer.PropagateFaults(faultSearch.faultDatas);
            List<FaultData> rootCauses = rootCauseAnalyzer.DetectRootCauses(faultSearch.faultDatas);

            FaultSearch.Led = rootCauses.Any();
            Console.WriteLine("LED: " + FaultSearch.Led);
            Console.WriteLine("Root fault(s): " + string.Join(", ", rootCauses.Select(fault => fault.Name)));
        }

        private void ResetFaultStatuses()
        {
            foreach (FaultData fault in faultSearch.faultDatas)
            {
                fault.FaultStatus = FaultStatus.WORKING;
            }
        }

        private bool IsOnlineState(string state)
        {
            return string.Equals(state?.Trim(), "ONLINE", StringComparison.OrdinalIgnoreCase);
        }

        public async Task TreeSearchW(string fault)
        {
            await Task.Run(() =>
            {
                FaultData faultData = faultSearch.faultDatas.FirstOrDefault(item => item.Name == fault);
                if (faultData != null)
                {
                    faultData.FaultStatus = FaultStatus.WORKING;
                }
            });
        }

        public async Task TreeSearchF(string fault)
        {
            await Task.Run(() =>
            {
                FaultData faultData = faultSearch.faultDatas.FirstOrDefault(item => item.Name == fault);
                if (faultData != null)
                {
                    faultData.FaultStatus = FaultStatus.FAULT;
                    Console.WriteLine(faultData.Name + " ÁTÁLLÍTVA FAULT ÁLLAPOTRA");
                }
            });
        }

        public async Task DiagnoseAnalyse()
        {
            bool mqttIsConnected = await GetMqttStatus();
            ResetFaultStatuses();

            // 1. SZINT: RENDSZER KOMMUNIKÁCIÓ ELLENŐRZÉSE
            if (diagnoses.KommRendszer.Data)
            {
                await TreeSearchF(FaultSearch.KommRendszer.Name);
                return;
            }

            // 2. SZINT: KOMMUNIKÁCIÓS KÖZPONT
            // Az MQTT elérhetetlensége magasabb szintű központi kommunikációs hibára képződik.
            if (!mqttIsConnected || diagnoses.KommKozpont.Data)
            {
                await TreeSearchF(FaultSearch.KommKozpont.Name);
                return;
            }

            // A KommKozpontUp önálló diagnosztikai jelként kezelhető, ha maga az MQTT státusz
            // és a központ általános kommunikációs állapota nem hibás.
            if (diagnoses.KommKozpontUp.Data)
            {
                await TreeSearchF(FaultSearch.KommKozpontUp.Name);
                return;
            }

            // 3. SZINT: PÁRHUZAMOS ESZKÖZ ELLENŐRZÉSEK
            // Ide csak akkor jutunk, ha a Rendszer és a Központ rendben van.

            // --- A) KOCSI (CAR) ---
            // A kocsi alatti ESP csak akkor számít külön mért hibának, ha maga a kocsi online.
            // Ha a kocsi offline, akkor a gyerek állapota nem megbízható, ezért azt az RCA
            // CONSEQUENCE-ként származtatja a KommKocsi hibából.
            string carstateTemp = await dashboardData.GetCarState();
            bool carOnline = IsOnlineState(carstateTemp);
            if (carOnline)
            {
                carstate = carstateTemp?.Trim();
                await TreeSearchW(FaultSearch.KommKocsi.Name);

                string bottleStateTemp = await dashboardData.GetBottlesState();
                if (IsOnlineState(bottleStateTemp))
                {
                    bottleState = bottleStateTemp?.Trim();
                    await TreeSearchW(FaultSearch.KommKocsiEsp.Name);
                }
                else
                {
                    bottleState = "OFFLINE";
                    await TreeSearchF(FaultSearch.KommKocsiEsp.Name);
                }
            }
            else
            {
                carstate = "OFFLINE";
                bottleState = "UNKNOWN";
                await TreeSearchF(FaultSearch.KommKocsi.Name);
            }

            // --- B) TARTÁLY (TANK) ---
            // A tartály alatti hibákat is csak akkor vesszük külön mért hibának, ha maga a
            // tartálykommunikáció működik. Offline tartálynál ezek következményként jelennek meg.
            string tankStateTemp = await dashboardData.GetTankState();
            bool tankOnline = IsOnlineState(tankStateTemp);
            if (tankOnline)
            {
                tankState = tankStateTemp?.Trim();
                await TreeSearchW(FaultSearch.KommTartaly.Name);

                if (diagnoses.AramTartaly.Data) await TreeSearchF(FaultSearch.AramTartaly.Name); else await TreeSearchW(FaultSearch.AramTartaly.Name);
                if (diagnoses.GyarSzalagSzenz.Data) await TreeSearchF(FaultSearch.GyarSzalagSzenz.Name); else await TreeSearchW(FaultSearch.GyarSzalagSzenz.Name);
            }
            else
            {
                tankState = "OFFLINE";
                await TreeSearchF(FaultSearch.KommTartaly.Name);
            }

            // --- C) RFID ---
            // Két fő hibaforrás: kommunikációs hiba és olvasási/rakományegyezési hiba.
            // A GyarRfidOlv csak akkor mért hiba, ha az RFID kommunikáció működik, de a rakomány nem egyezik.
            // Ha az RFID olvasók nem elérhetők, a GyarRfidOlv CONSEQUENCE lesz a KommRfidUp alatt.
            if (diagnoses.KommRfidUp.Data)
            {
                await TreeSearchF(FaultSearch.KommRfidUp.Name);
            }
            else
            {
                await TreeSearchW(FaultSearch.KommRfidUp.Name);

                if (diagnoses.GyarRfidOlv.Data)
                {
                    await TreeSearchF(FaultSearch.GyarRfidOlv.Name);
                }
                else
                {
                    await TreeSearchW(FaultSearch.GyarRfidOlv.Name);
                }
            }

            // --- EGYÉB HIBÁK AUTOMATIKUS FRISSÍTÉSE ---
            // Minden olyan hiba, amit manuálisan nem fedtünk le fentebb. Itt már nincs
            // hierarchikus terjesztés: csak az adott diagnosztikai bemenet saját állapotát írjuk.
            // Az offline szülő alatti gyermekeket szándékosan kihagyjuk, hogy az RCA CONSEQUENCE-ként jelölje őket.
            foreach (PropertyInfo prop in
                    typeof(Diagnoses)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(DiagnoseData)))
            {
                DiagnoseData currentDiagnose = (DiagnoseData)prop.GetValue(diagnoses, null);

                if (ShouldSkipGenericDiagnose(currentDiagnose.Name, carOnline, tankOnline))
                {
                    continue;
                }

                if (currentDiagnose.Data)
                {
                    await TreeSearchF(currentDiagnose.Name);
                }
            }
        }

        private bool ShouldSkipGenericDiagnose(string diagnoseName, bool carOnline, bool tankOnline)
        {
            if (diagnoseName == FaultSearch.KommRendszer.Name ||
                diagnoseName == FaultSearch.KommKozpont.Name ||
                diagnoseName == FaultSearch.KommKozpontUp.Name ||
                diagnoseName == FaultSearch.KommKocsi.Name ||
                diagnoseName == FaultSearch.KommKocsiEsp.Name ||
                diagnoseName == FaultSearch.KommTartaly.Name ||
                diagnoseName == FaultSearch.KommRfidUp.Name ||
                diagnoseName == FaultSearch.GyarRfidOlv.Name)
            {
                return true;
            }

            if (!carOnline &&
                (diagnoseName == FaultSearch.GyarTargoncaSzenz.Name ||
                 diagnoseName == FaultSearch.AramKocsi.Name ||
                 diagnoseName == FaultSearch.KommTargoncaArammero.Name))
            {
                return true;
            }

            if (!tankOnline &&
                (diagnoseName == FaultSearch.AramTartaly.Name ||
                 diagnoseName == FaultSearch.GyarSzalagSzenz.Name ||
                 diagnoseName == FaultSearch.GyarTartalySzenz.Name ||
                 diagnoseName == FaultSearch.KommTartalyArammero.Name))
            {
                return true;
            }

            return false;
        }

        public async Task<bool> GetMqttStatus()
        {
            var status = await dashboardData.GetMqttStatus();
            Console.WriteLine("KOMM STATUS: " + status);
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
