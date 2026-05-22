
using DiagnoseDashboard.Model;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnoseDashboard.Data
{
    public interface IDashboardData
    {
        [Get("/api/Diagnose/Dashboard/GetDiagnoses")]
        Task<Diagnoses> GetDiagnoses();

        [Post("/api/Diagnose/MQTTConnection")]
        Task ConnectMQTT();

        [Get("/api/Diagnose/Dashboard/GetCarState")]
        Task<string> GetCarState();

        [Get("/api/Diagnose/Dashboard/GetTankState")]
        Task<string> GetTankState();

        [Get("/api/Diagnose/Dashboard/GetBottlesState")]
        Task<string> GetBottlesState();

        [Get("/api/Diagnose/Dashboard/GetRfidStatus")]
        Task<RfidStatus> GetRfidStatus();

        [Post("/api/Diagnose/MQTTConnectionLost")]
        Task<bool> GetLostConnection();

        [Post("/api/Diagnose/IfFailure")]
        Task<bool> LEDRed(bool value);

        [Get("/api/Diagnose/Dashboard/MQTTIsConnected")]
        Task<bool> MqttIsConnected();
        [Get("/api/Diagnose/Dashboard/GetMqttStatus")]
        Task<bool> GetMqttStatus();
    }
}
