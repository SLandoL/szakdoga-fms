using FactorySimulation.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Refit;
using FactorySimulation.DataAccess;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FactorySimulation.Model;

namespace FactorySimulation.ViewModels
{
    public class ControlPanelViewModel
    {
        public static int CarSpeed { get; set; } = 50;
        public static bool RendszerButton { get; set; } = false;
        public static bool PauseButton { get; set; } = false;
        public static bool WakeUpButton { get; set; } = false;
        public static bool SzalagPLCButton { get; set; } = false;
        public static bool RFIDButton { get; set; } = false;
        public static bool RendszerElectroButton { get; set; } = false;
        public static bool KKElectroHibaButton { get; set; } = false;
        public static bool TartalyElectroButton { get; set; } = false;
        public static bool KocsiElectroButton { get; set; } = false;
        public static bool KocsiSzenzorButton { get; set; } = false;
        public static bool KommKozElectroKommButton { get; set; } = false;
        public static bool TartalyElectroKommButton { get; set; } = false;
        public static bool KocsiElectroKommButton { get; set; } = false;
        public static bool TartalyButton { get; set; } = false;
        public static LEDColor LEDColor { get; set; } = LEDColor.BLUE;
        public static bool Ready { get; set; } = false;

        private ControlPanelService controlPanelService = new ControlPanelService();

        public async Task PostCarSpeed(int number)
        {
            CarSpeed = CarSpeed + number;
            if (CarSpeed < 0)
            {
                CarSpeed = 0;
                return;
            }
            else if (CarSpeed > 100)
            {
                CarSpeed = 100;
                return;
            }
            await controlPanelService.PostCarSpeedAsync(CarSpeed);
        }
        public async Task PostSystem()
        {
            RendszerButton = !RendszerButton;
            await controlPanelService.PostSystemAsync(RendszerButton);
            await StopCar();
        }
        public async Task PostPause()
        {
            PauseButton = !PauseButton;
            await controlPanelService.PostPauseAsync(PauseButton);
            await StopCar();
        }
        public async Task PostWakeUp()
        {
            WakeUpButton = true;
            await controlPanelService.PostWakeUpAsync(WakeUpButton);
            await Task.Delay(1000);
            WakeUpButton = false;
            
        }
        public async Task PostPLCFailure()
        {
            SzalagPLCButton = !SzalagPLCButton;
            await controlPanelService.PostPLCFailureAsync(SzalagPLCButton);
            await StopCar();
        }
        public async Task PostRFIDError()
        {
            RFIDButton = !RFIDButton;
            await controlPanelService.PostRFIDAsnyc(RFIDButton);
            await StopCar();
        }
        public async Task PostRendszerElectroError()
        {
            RendszerElectroButton = !RendszerElectroButton;
            await controlPanelService.PostRendszerElectroAsync(RendszerElectroButton);
            await StopCar();
        }
        public async Task PostTartalyElectroError()
        {
            TartalyElectroButton = !TartalyElectroButton;
            await controlPanelService.PostTartalyElectoAsync(TartalyElectroButton);
            await StopCar();
        }
        public async Task PostKocsiElectroError()
        {
            KocsiElectroButton = !KocsiElectroButton;
            await controlPanelService.PostKocsiElectroAsync(KocsiElectroButton);
            await StopCar();
        }
        public async Task PostCommunicationPowerError()
        {
            KKElectroHibaButton = !KKElectroHibaButton;
            await controlPanelService.PostCommunicationPowerErrorAsync(KKElectroHibaButton);
            await StopCar();
        }
        public async Task PostCarError()
        {
            KocsiSzenzorButton = !KocsiSzenzorButton;
            await controlPanelService.PostCarErrorAsync(KocsiSzenzorButton);
            await StopCar();
        }
        public async Task PostContainerEmpty()
        {
            TartalyButton = !TartalyButton;
            await controlPanelService.PostContainerEmptyAsync(TartalyButton);
            await StopCar();
        }
        public async Task PostKommKozElectroKommError()
        {
            KommKozElectroKommButton = !KommKozElectroKommButton;
            await controlPanelService.PostKommKozElectoKommAsync(KommKozElectroKommButton);
            await StopCar();
        }

        public async Task PostTartalyElectroKommError()
        {
            TartalyElectroKommButton = !TartalyElectroKommButton;
            await controlPanelService.PostTartalyElectoKommAsync(TartalyElectroKommButton);
            await StopCar();
        }

        public async Task PostKocsiElectroKommError()
        {
            KocsiElectroKommButton = !KocsiElectroKommButton;
            await controlPanelService.PostKocsiElectoKommAsync(KocsiElectroKommButton);
            await StopCar();
        }

        public async Task PostLED(LEDColor ledColor)
        {
            LEDColor = ledColor;
            await controlPanelService.PostLEDAsync(LEDColor.ToString());
        }

        public async Task GetDiagnoses()
        {
            Diagnoses DiagnosesFromDb = await controlPanelService.GetDiagnosesFromDBAsync();

            CarSpeed = int.Parse(DiagnosesFromDb.CarSpeed);
            RendszerButton = DiagnosesFromDb.System;
            PauseButton = DiagnosesFromDb.Pause;
            SzalagPLCButton = DiagnosesFromDb.PLCFailure;
            RFIDButton = DiagnosesFromDb.RFID;
            RendszerElectroButton = DiagnosesFromDb.RendszerElectro;
            TartalyElectroButton = DiagnosesFromDb.TartalyElectro;
            KocsiElectroButton = DiagnosesFromDb.KocsiElectro;
            KKElectroHibaButton = DiagnosesFromDb.CommunicationPowerError;
            KocsiSzenzorButton = DiagnosesFromDb.CarError;
            TartalyButton = DiagnosesFromDb.ContainerEmpty;
            KocsiElectroKommButton = DiagnosesFromDb.KocsiElectroKomm;
            TartalyElectroKommButton = DiagnosesFromDb.TartalyElectroKomm;
            KommKozElectroKommButton = DiagnosesFromDb.KommKozElectroKomm;
            LEDColor = Enum.Parse<LEDColor>(DiagnosesFromDb.LED);
            await StopCar();
            Ready = true;
        }

        private async Task StopCar()
        {
            if (RendszerButton || PauseButton || SzalagPLCButton || RFIDButton ||
                KKElectroHibaButton || KocsiSzenzorButton || TartalyButton || 
                KocsiElectroButton || TartalyElectroButton || RendszerElectroButton
                || KommKozElectroKommButton || TartalyElectroKommButton || KocsiElectroKommButton)
            {
                await controlPanelService.PostStopCar(true);
            }
            else
            {
                await controlPanelService.PostStopCar(false);
            }
        }

        public bool CarCanGo()
        {
            if (RendszerButton || PauseButton || SzalagPLCButton || RFIDButton || KKElectroHibaButton || KocsiSzenzorButton || TartalyButton || KocsiElectroButton || TartalyElectroButton || RendszerElectroButton)
            {
                return true;
            }
            return false;
        }
    }
}
