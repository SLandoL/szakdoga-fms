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
        private static RfidStatus rfidStatus = new RfidStatus();
        private static string baseUrl = "https://localhost:5004/";
        private IDashboardData dashboardData = RestService.For<IDashboardData>(baseUrl);
        private static bool connection;
        private static bool print;
        public ProfileMessages alert = ProfileMessages.NULL;
        public ProfileMessages answer = ProfileMessages.NULL;
        private FaultSearch faultSearch;
        private readonly RootCauseAnalyzer rootCauseAnalyzer;

        public RfidStatus CurrentRfidStatus => rfidStatus;

        public DiagnoseDashboardService(FaultSearch FaultSearch, RootCauseAnalyzer RootCauseAnalyzer)
        {
            faultSearch = FaultSearch;
            rootCauseAnalyzer = RootCauseAnalyzer;
        }

        public async Task GetDiagnosesAsync()
        {
            // RefreshRfidStatus triggers the DiagnoseService-side RFID timeout evaluation first.
            // The following GetDiagnoses call therefore receives the current KommRfidUp/GyarRfidOlv values.
            await RefreshRfidStatus();
            diagnoses = await dashboardData.GetDiagnoses();
            await DiagnoseAnalyse();
            RunRootCauseAnalysis();
        }

        private async Task RefreshRfidStatus()
        {
            try
            {
                rfidStatus = await dashboardData.GetRfidStatus();
            }
            catch (Exception ex)
            {
                rfidStatus.DiagnosticSummary = "RFID status endpoint is not available: " + ex.Message;
            }
        }

        private void RunRootCauseAnalysis()
        {
            rootCauseAnalyzer.ResetAnalysisStatuses(faultSearch.faultDatas);
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

        private void SetFaultStatus(string faultName, FaultStatus status)
        {
            FaultData faultData = faultSearch.faultDatas.FirstOrDefault(item => item.Name == faultName);
            if (faultData == null)
            {
                return;
            }

            faultData.FaultStatus = status;

            if (status == FaultStatus.FAULT)
            {
                Console.WriteLine(faultData.Name + " ÁTÁLLÍTVA FAULT ÁLLAPOTRA");
            }
        }

        private void MarkWorking(string faultName)
        {
            SetFaultStatus(faultName, FaultStatus.WORKING);
        }

        private void MarkFault(string faultName)
        {
            SetFaultStatus(faultName, FaultStatus.FAULT);
        }

        public async Task DiagnoseAnalyse()
        {
            bool mqttIsConnected = await GetMqttStatus();
            ResetFaultStatuses();

            bool systemCommunicationFault = AnalyseSystemCommunication();
            bool centerCommunicationFault = false;

            if (!systemCommunicationFault)
            {
                centerCommunicationFault = AnalyseMqttCenter(mqttIsConnected);
            }

            // Ha felsőbb szintű kommunikációs hiba aktív, az eszközállapot-lekérdezések
            // nem feltétlenül megbízhatóak. A közvetlenül beérkezett diagnózisbemeneteket
            // viszont ekkor is meg kell őrizni FAULT állapotként, különben az RCA tévesen
            // CONSEQUENCE-ként jelenítené meg a ténylegesen mért alsóbb hibákat.
            if (systemCommunicationFault || centerCommunicationFault)
            {
                PreserveMeasuredDiagnoseInputs();
                return;
            }

            bool carOnline = await AnalyseCar();
            bool tankOnline = await AnalyseTank();

            AnalyseRfid();
            AnalyseRemainingDiagnoses(carOnline, tankOnline);

            // Utolsó védőlépés: minden explicit mért diagnózisbemenet maradjon FAULT.
            // Így a hierarchikus propagation csak a valóban nem mért gyermekhibákat
            // állíthatja CONSEQUENCE állapotba.
            PreserveMeasuredDiagnoseInputs();
        }

        private bool AnalyseSystemCommunication()
        {
            if (!diagnoses.KommRendszer.Data)
            {
                return false;
            }

            MarkFault(FaultSearch.KommRendszer.Name);
            return true;
        }

        private bool AnalyseMqttCenter(bool mqttIsConnected)
        {
            // Az MQTT elérhetetlensége magasabb szintű központi kommunikációs hibára képződik.
            if (!mqttIsConnected || diagnoses.KommKozpont.Data)
            {
                MarkFault(FaultSearch.KommKozpont.Name);
                return true;
            }

            // A KommKozpontUp önálló diagnosztikai jelként kezelhető, ha maga az MQTT státusz
            // és a központ általános kommunikációs állapota nem hibás.
            if (diagnoses.KommKozpontUp.Data)
            {
                MarkFault(FaultSearch.KommKozpontUp.Name);
                return true;
            }

            return false;
        }

        private async Task<bool> AnalyseCar()
        {
            // A kocsi alatti ESP csak akkor számít külön mért hibának, ha maga a kocsi online.
            // Ha a kocsi offline, akkor a gyerek állapota nem megbízható, ezért azt az RCA
            // CONSEQUENCE-ként származtatja a KommKocsi hibából, kivéve ha az adott gyereknek
            // közvetlen diagnózisbemenete is hibát jelez.
            string carstateTemp = await dashboardData.GetCarState();
            bool carOnline = IsOnlineState(carstateTemp);

            if (carOnline)
            {
                carstate = carstateTemp?.Trim();
                MarkWorking(FaultSearch.KommKocsi.Name);
                await AnalyseBottle();
            }
            else
            {
                carstate = "OFFLINE";
                bottleState = "UNKNOWN";
                MarkFault(FaultSearch.KommKocsi.Name);
            }

            return carOnline;
        }

        private async Task AnalyseBottle()
        {
            string bottleStateTemp = await dashboardData.GetBottlesState();
            if (IsOnlineState(bottleStateTemp))
            {
                bottleState = bottleStateTemp?.Trim();
                MarkWorking(FaultSearch.KommKocsiEsp.Name);
            }
            else
            {
                bottleState = "OFFLINE";
                MarkFault(FaultSearch.KommKocsiEsp.Name);
            }
        }

        private async Task<bool> AnalyseTank()
        {
            // A tartály alatti hibákat is csak akkor vesszük külön mért hibának, ha maga a
            // tartálykommunikáció működik. Offline tartálynál ezek következményként jelennek meg,
            // kivéve ha az adott gyerekhez közvetlen diagnózisbemenet is hibát jelez.
            string tankStateTemp = await dashboardData.GetTankState();
            bool tankOnline = IsOnlineState(tankStateTemp);

            if (tankOnline)
            {
                tankState = tankStateTemp?.Trim();
                MarkWorking(FaultSearch.KommTartaly.Name);

                if (diagnoses.AramTartaly.Data) MarkFault(FaultSearch.AramTartaly.Name); else MarkWorking(FaultSearch.AramTartaly.Name);
                if (diagnoses.GyarSzalagSzenz.Data) MarkFault(FaultSearch.GyarSzalagSzenz.Name); else MarkWorking(FaultSearch.GyarSzalagSzenz.Name);
            }
            else
            {
                tankState = "OFFLINE";
                MarkFault(FaultSearch.KommTartaly.Name);
            }

            return tankOnline;
        }

        private void AnalyseRfid()
        {
            // Két fő hibaforrás: kommunikációs hiba és olvasási/rakományegyezési hiba.
            // Ha mindkettő ténylegesen beérkezik mért hibaként, akkor a GyarRfidOlv FAULT marad,
            // de az RCA miatt nem lesz külön ROOTFAULT a KommRfidUp alatt.
            bool rfidCommunicationFault = diagnoses.KommRfidUp.Data;
            bool rfidReadingFault = diagnoses.GyarRfidOlv.Data;

            if (rfidCommunicationFault)
            {
                MarkFault(FaultSearch.KommRfidUp.Name);
            }
            else
            {
                MarkWorking(FaultSearch.KommRfidUp.Name);
            }

            if (rfidReadingFault)
            {
                MarkFault(FaultSearch.GyarRfidOlv.Name);
            }
            else if (!rfidCommunicationFault)
            {
                MarkWorking(FaultSearch.GyarRfidOlv.Name);
            }
        }

        private void AnalyseRemainingDiagnoses(bool carOnline, bool tankOnline)
        {
            // Minden olyan hiba, amit manuálisan nem fedtünk le fentebb. Itt már nincs
            // hierarchikus terjesztés: csak az adott diagnosztikai bemenet saját állapotát írjuk.
            // Ha egy offline szülő alatti gyereknek nincs saját hibajele, az RCA CONSEQUENCE-ként jelöli.
            // Ha van saját hibajele, akkor FAULT marad, nem írhatja felül a következményterjesztés.
            foreach (PropertyInfo prop in
                    typeof(Diagnoses)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(DiagnoseData)))
            {
                DiagnoseData currentDiagnose = (DiagnoseData)prop.GetValue(diagnoses, null);

                if (currentDiagnose == null || ShouldSkipGenericDiagnose(currentDiagnose.Name))
                {
                    continue;
                }

                if (currentDiagnose.Data)
                {
                    MarkFault(currentDiagnose.Name);
                }
            }
        }

        private void PreserveMeasuredDiagnoseInputs()
        {
            foreach (PropertyInfo prop in
                    typeof(Diagnoses)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(DiagnoseData)))
            {
                DiagnoseData currentDiagnose = (DiagnoseData)prop.GetValue(diagnoses, null);

                if (currentDiagnose == null || string.IsNullOrWhiteSpace(currentDiagnose.Name))
                {
                    continue;
                }

                if (currentDiagnose.Data)
                {
                    MarkFault(currentDiagnose.Name);
                }
            }
        }

        private bool ShouldSkipGenericDiagnose(string diagnoseName)
        {
            return diagnoseName == FaultSearch.KommRendszer.Name ||
                   diagnoseName == FaultSearch.KommKozpont.Name ||
                   diagnoseName == FaultSearch.KommKozpontUp.Name ||
                   diagnoseName == FaultSearch.KommKocsi.Name ||
                   diagnoseName == FaultSearch.KommKocsiEsp.Name ||
                   diagnoseName == FaultSearch.KommTartaly.Name ||
                   diagnoseName == FaultSearch.KommRfidUp.Name ||
                   diagnoseName == FaultSearch.GyarRfidOlv.Name;
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
                    MarkFault(FaultSearch.KommKozpont.Name);
                }
            }
            else
            {
                answer = ProfileMessages.MQTTISREACHABLE;
                Console.WriteLine("ConnectMQTT Yes: " + answer);
                try
                {
                    await MQTTConnectionAsync();
                    MarkWorking(FaultSearch.KommKozpont.Name);
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
