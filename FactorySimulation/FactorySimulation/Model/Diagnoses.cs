using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactorySimulation.Model
{
    public class Diagnoses
    {
        public string CarSpeed { get; set; }
        public bool System { get; set; }
        public bool Pause { get; set; }
        public bool WakeUp { get; set; }
        public bool PLCFailure { get; set; }
        public bool RFID { get; set; }
        public bool RendszerElectro { get; set; }
        public bool TartalyElectro { get; set; }
        public bool KocsiElectro { get; set; }
        public bool CommunicationPowerError { get; set; }
        public bool KommKozElectroKomm { get; set; }
        public bool TartalyElectroKomm { get; set; }
        public bool KocsiElectroKomm { get; set; }
        public bool CarError { get; set; }
        public bool ContainerEmpty { get; set; }
        public string LED { get; set; }

        public override string ToString()
        {
            return CarSpeed + System + Pause + WakeUp + PLCFailure + RFID + RendszerElectro + TartalyElectro + KocsiElectro + CommunicationPowerError + KommKozElectroKomm + TartalyElectroKomm + KocsiElectroKomm + CarError + ContainerEmpty + LED;
        }
    }
}
