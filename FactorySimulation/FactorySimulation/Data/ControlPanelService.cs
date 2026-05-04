using FactorySimulation.DataAccess;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FactorySimulation.Model;

namespace FactorySimulation.Data
{
    public class ControlPanelService
    {
        private static string baseUrl = "https://localhost:5002/";
        private IFactoryData factoryData = RestService.For<IFactoryData>(baseUrl);

        //private static string baseUrl = "https://172.20.0.235:44326";
        public async Task PostCarSpeedAsync(int value)
        {
            await factoryData.PostCarSpeed(value.ToString());
        }
        public async Task PostSystemAsync(bool value)
        {
            await factoryData.PostSystem(value);
        }
        public async Task PostPauseAsync(bool value)
        {
            await factoryData.PostPause(value);
        }
        public async Task PostWakeUpAsync(bool value)
        {
            await factoryData.PostWakeUp(value);
        }
        public async Task PostPLCFailureAsync(bool value)
        {
            await factoryData.PostPLCFailure(value);
        }
        public async Task PostCommunicationPowerErrorAsync(bool value)
        {
            await factoryData.PostCommunicationPowerError(value);
        }
        public async Task PostCarErrorAsync(bool value)
        {
            await factoryData.PostCarError(value);
        }
        public async Task PostContainerEmptyAsync(bool value)
        {
            await factoryData.PostContainerEmpty(value);
        }
        public async Task PostLEDAsync(string value)
        {
            await factoryData.PostLED(value);
        }

        public async Task PostStopCar(bool value)
        {
            await factoryData.StopCar(value);
        }

        public async Task<Diagnoses> GetDiagnosesFromDBAsync()
        {
            return await factoryData.GetDiagnosesFromDB();
        }

        public async Task PostRFIDAsnyc(bool value)
        {
            await factoryData.PostRFID(value);
        }

        public async Task PostRendszerElectroAsync(bool value)
        {
            await factoryData.PostRendszerElectro(value);
        }
        public async Task PostTartalyElectoAsync(bool value)
        {
            await factoryData.PostTartalyElectro(value);
        }
        public async Task PostKocsiElectroAsync(bool value)
        {
            await factoryData.PostKocsElectro(value);
        }

        public async Task PostKommKozElectoKommAsync(bool value)
        {
            await factoryData.PostKommKozElectroKomm(value);
        }

        public async Task PostTartalyElectoKommAsync(bool value)
        {
            await factoryData.PostTartalyElectroKomm(value);
        }

        public async Task PostKocsiElectoKommAsync(bool value)
        {
            await factoryData.PostKocsiElectroKomm(value);
        }
    }
}
