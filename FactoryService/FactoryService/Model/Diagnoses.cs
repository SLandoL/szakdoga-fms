using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnoseService.Model
{
    public class Diagnoses
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string CarSpeed { get; set; }
        [Required]
        public bool System { get; set; }
        [Required]
        public bool Pause { get; set; }
        [Required]
        public bool WakeUp { get; set; }
        [Required]
        public bool PLCFailure { get; set; }
        [Required]
        public bool RFID { get; set; }
        [Required]
        public bool RendszerElectro { get; set; }
        [Required]
        public bool TartalyElectro { get; set; }
        [Required]
        public bool KocsiElectro { get; set; }
        [Required]
        public bool CommunicationPowerError { get; set; }
        [Required]
        public bool KommKozElectroKomm { get; set; }
        [Required]
        public bool TartalyElectroKomm { get; set; }
        [Required]
        public bool KocsiElectroKomm { get; set; }
        [Required]
        public bool CarError { get; set; }
        [Required]
        public bool ContainerEmpty { get; set; }
        [Required]
        [MaxLength(20)]
        public string LED { get; set; }

        public Diagnoses(){}
        public Diagnoses(string carSpeed, string led, bool system = false, bool pause = false, bool wakeUp = false, bool plcFailure = false, bool rfid = false, bool rendszerElecro = false, bool tartalyElectro = false,
                                        bool kocsiElectro = false, bool communicationPowerError = false, bool kommkozelectrokomm = false, bool tartalyelectrokomm = false,
                                        bool kocsielectrokomm = false, bool carError = false, bool containerEmpty = false)
        {
            CarSpeed = carSpeed;
            System = system;
            Pause = pause;
            WakeUp = wakeUp;
            PLCFailure = plcFailure;
            RFID = rfid;
            RendszerElectro = rendszerElecro;
            TartalyElectro = tartalyElectro;
            KocsiElectro = kocsiElectro;
            CommunicationPowerError = communicationPowerError;
            KommKozElectroKomm = kommkozelectrokomm;
            TartalyElectroKomm = tartalyelectrokomm;
            KocsiElectroKomm = kocsielectrokomm;
            CarError = carError;
            ContainerEmpty = containerEmpty;
            LED = led;

        }
        public override string ToString()
        {
            return CarSpeed + "," + System + "," + Pause
                + "," + WakeUp + "," + PLCFailure 
                + "," + RFID
                + "," + RendszerElectro
                + "," + TartalyElectro
                + "," + KocsiElectro
                + "," + CommunicationPowerError
                + "," + KommKozElectroKomm
                + "," + TartalyElectroKomm
                + "," + KocsiElectroKomm
                + "," + CarError + "," + ContainerEmpty 
                + "," + LED;
        }
    }
}
