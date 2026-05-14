using System;

namespace DiagnoseService.Data
{
    public class RfidStatus
    {
        public string DeviceId { get; set; } = "rfid-esp";
        public bool EspOnline { get; set; }
        public bool TankReaderOk { get; set; }
        public bool WarehouseReaderOk { get; set; }
        public bool TankReaderFresh { get; set; }
        public bool WarehouseReaderFresh { get; set; }
        public bool CargoMatch { get; set; } = true;
        public bool CargoMatchKnown { get; set; }
        public string TankCargoId { get; set; } = string.Empty;
        public string WarehouseCargoId { get; set; } = string.Empty;
        public int TankReaderErrorCode { get; set; }
        public int WarehouseReaderErrorCode { get; set; }
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.MinValue;
        public DateTime LastTankReaderStatusUtc { get; set; } = DateTime.MinValue;
        public DateTime LastWarehouseReaderStatusUtc { get; set; } = DateTime.MinValue;
        public DateTime LastCargoReadUtc { get; set; } = DateTime.MinValue;
        public string DiagnosticSummary { get; set; } = "RFID heartbeat has not been received yet.";
    }
}
