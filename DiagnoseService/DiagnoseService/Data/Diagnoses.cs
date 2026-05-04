using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnoseService.Data
{
    public class Diagnoses
    {
        public DiagnoseData KommRendszer { get; set; } = new DiagnoseData("KommRendszer");
        public DiagnoseData GyarSzalagSzenz { get; set; } = new DiagnoseData("GyarSzalagSzenz");
        public DiagnoseData GyarRfidOlv { get; set; } = new DiagnoseData("GyarRfidOlv");
        public DiagnoseData KommRfidUp { get; set; } = new DiagnoseData("KommRfidUp");
        public DiagnoseData AramRendszer { get; set; } = new DiagnoseData("AramRendszer");
        public DiagnoseData KommKozpont { get; set; } = new DiagnoseData("KommKozpont");
        public DiagnoseData KommKozpontUp { get; set; } = new DiagnoseData("KommKozpontUp");
        public DiagnoseData AramTartaly { get; set; } = new DiagnoseData("AramTartaly");
        public DiagnoseData AramKocsi { get; set; } = new DiagnoseData("AramKocsi");
        public DiagnoseData AramKommKozpont { get; set; } = new DiagnoseData("AramKommKozpont");
        public DiagnoseData KommKommArammero { get; set; } = new DiagnoseData("KommKommArammero");
        public DiagnoseData KommTartalyArammero { get; set; } = new DiagnoseData("KommTartalyArammero");
        public DiagnoseData KommTargoncaArammero { get; set; } = new DiagnoseData("KommTargoncaArammero");
        public DiagnoseData GyarTargoncaSzenz { get; set; } = new DiagnoseData("GyarTargoncaSzenz");
        public DiagnoseData GyarTartalySzenz { get; set; } = new DiagnoseData("GyarTartalySzenz");
    }
}
