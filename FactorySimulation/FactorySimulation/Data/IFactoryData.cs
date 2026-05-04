using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FactorySimulation.Data;
using FactorySimulation.Model;
using Refit;

namespace FactorySimulation.DataAccess
{
    public interface IFactoryData
    {
        [Post("/api/Factory/ControlPanel/CarSpeed")]
        Task<string> PostCarSpeed(string value);

        [Post("/api/Factory/ControlPanel/System")]
        Task<string> PostSystem(bool value);

        [Post("/api/Factory/ControlPanel/Pause")]
        Task<string> PostPause(bool value);

        [Post("/api/Factory/ControlPanel/WakeUp")]
        Task<string> PostWakeUp(bool value);

        [Post("/api/Factory/ControlPanel/PLCFailure")]
        Task<string> PostPLCFailure(bool value);

        [Post("/api/Factory/ControlPanel/CommunicationPowerError")]
        Task<string> PostCommunicationPowerError(bool value);

        [Post("/api/Factory/ControlPanel/CarError")]
        Task<string> PostCarError(bool value);

        [Post("/api/Factory/ControlPanel/ContainerEmpty")]
        Task<string> PostContainerEmpty(bool value);

        [Post("/api/Factory/ControlPanel/LED")]
        Task<string> PostLED(string value);

        [Get("/api/Factory/DigitalFactory/CarLocation")]
        Task<string> GetCarLocation();

        [Post("/api/Factory/MQTTConnection")]
        Task ConnectMQTT();

        [Get("/api/Factory/ControlPanel/GetDiagnosesFromDB")]
        Task<Diagnoses> GetDiagnosesFromDB();

        [Post("/api/Factory/MQTTConnectionLost")]
        Task<bool> GetLostConnection();

        [Post("/api/Factory/ControlPanel/StopCar")]
        Task<string> StopCar(bool value);

        [Post("/api/Factory/ControlPanel/RFID")]
        Task<string> PostRFID(bool value);

        [Post("/api/Factory/ControlPanel/RendszerElectro")]
        Task<string> PostRendszerElectro(bool value);

        [Post("/api/Factory/ControlPanel/TartalyElectro")]
        Task<string> PostTartalyElectro(bool value);

        [Post("/api/Factory/ControlPanel/KocsiElectro")]
        Task<string> PostKocsElectro(bool value);

        [Post("/api/Factory/ControlPanel/KommKozElectroKomm")]
        Task<string> PostKommKozElectroKomm(bool value);

        [Post("/api/Factory/ControlPanel/TartalyElectroKomm")]
        Task<string> PostTartalyElectroKomm(bool value);

        [Post("/api/Factory/ControlPanel/KocsiElectroKomm")]
        Task<string> PostKocsiElectroKomm(bool value);
    }
}
