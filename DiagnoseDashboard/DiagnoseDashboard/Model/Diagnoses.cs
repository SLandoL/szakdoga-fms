using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnoseDashboard.Model
{
    public class Diagnoses
    {
        public DiagnoseData KommRendszer { get; set; }
        public DiagnoseData GyarSzalagSzenz { get; set; }
        public DiagnoseData GyarRfidOlv { get; set; }
        public DiagnoseData KommRfidUp { get; set; }
        public DiagnoseData KommKozpont { get; set; }
        public DiagnoseData KommKozpontUp { get; set; }
        public DiagnoseData AramRendszer { get; set; }
        public DiagnoseData AramTartaly { get; set; }
        public DiagnoseData AramKocsi { get; set; }
        public DiagnoseData AramKommKozpont { get; set; }
        public DiagnoseData KommKommArammero { get; set; }
        public DiagnoseData KommTartalyArammero { get; set; }
        public DiagnoseData KommTargoncaArammero { get; set; }
        public DiagnoseData GyarTargoncaSzenz { get; set; }
        public DiagnoseData GyarTartalySzenz { get; set; }
        public override string ToString()
        {
            return $"{KommRendszer} + {GyarSzalagSzenz} + {GyarRfidOlv} + {AramRendszer} + {AramTartaly} + {AramKommKozpont} + {KommKommArammero} + {KommTartalyArammero} + {KommTargoncaArammero} + {GyarTargoncaSzenz} + {GyarTartalySzenz}";
        }
    }
}
