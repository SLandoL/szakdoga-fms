using DiagnoseService.Model;
using FactoryService.DataAccess;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FactoryService.Model;
using Newtonsoft.Json;
using System.Reflection;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FactoryService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FactoryController : ControllerBase
    {
        private DiagnoseContext db;
        public static Diagnoses diagnoses = new Diagnoses("50", "BLUE");
        public static string CarLocation { get; set; } = "";
        private MQTTPublisher mQTTPublisher = new MQTTPublisher();

        public FactoryController(DiagnoseContext _db)
        {
            db = _db;
            var data = db.diagnoses.FirstOrDefault();
            diagnoses = new Diagnoses
            {
                CarSpeed = data.CarSpeed,
                System = data.System,
                Pause = data.Pause,
                WakeUp = data.WakeUp,
                PLCFailure = data.PLCFailure,
                RFID = data.RFID,
                RendszerElectro = data.RendszerElectro,
                TartalyElectro = data.TartalyElectro,
                KocsiElectro = data.KocsiElectro,
                CommunicationPowerError = data.CommunicationPowerError,
                KommKozElectroKomm = data.KommKozElectroKomm,
                TartalyElectroKomm = data.TartalyElectroKomm,
                KocsiElectroKomm = data.KocsiElectroKomm,
                CarError = data.CarError,
                ContainerEmpty = data.ContainerEmpty,
                LED = data.LED
            };
        }

        private async Task SaveDiagnosesToDB()
        {
            if (db.diagnoses.FirstOrDefault() == null)
            {
                await db.AddAsync(diagnoses);
            }
            else
            {
                db.diagnoses.Remove(db.diagnoses.FirstOrDefault());
                await db.AddAsync(diagnoses);
            }

            await db.SaveChangesAsync();
        }

        // GET: api/<FactoryController>
        [HttpPost]
        [Route("ControlPanel/CarSpeed")]
        public async Task PostCarSpeed(string value)
        {
            diagnoses.CarSpeed = value;
            string carSpeed = "carSpeed" + "," + diagnoses.CarSpeed;
            await SaveDiagnosesToDB();
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, carSpeed);
        }

        [HttpPost]
        [Route("ControlPanel/System")]
        public async Task PostSystem(bool value)
        {
            diagnoses.System = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("KommRendszer", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/Pause")]
        public async Task PostPauseAsync(bool value)
        {
            diagnoses.Pause = value;
            string pause = "Paused" + "," + diagnoses.Pause;
            await SaveDiagnosesToDB();
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, pause);
        }

        [HttpPost]
        [Route("ControlPanel/WakeUp")]
        public async Task PostWakeUpAsync(bool value)
        {
            diagnoses.WakeUp = value;
            string wakeUp = "WakeUp" + "," + diagnoses.WakeUp;
            await SaveDiagnosesToDB();
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, wakeUp);
        }

        [HttpPost]
        [Route("ControlPanel/PLCFailure")]
        public async Task PostPLCFailureAsync(bool value)
        {
            diagnoses.PLCFailure = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("GyarSzalagSzenz", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/CommunicationPowerError")]
        public async Task PostCommunicationPowerErrorAsync(bool value)
        {
            diagnoses.CommunicationPowerError = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("AramKommKozpont", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/CarError")]
        public async Task PostCarErrorAsync(bool value)
        {
            diagnoses.CarError = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("GyarTargoncaSzenz", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/ContainerEmpty")]
        public async Task PostContainerEmptyAsync(bool value)
        {
            diagnoses.ContainerEmpty = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("GyarTartalySzenz", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/RFID")]
        public async Task PostRFIDAsync(bool value)
        {
            diagnoses.RFID = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("GyarRfidOlv", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/RendszerElectro")]
        public async Task PostRendszerElectroAsync(bool value)
        {
            diagnoses.RendszerElectro = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("AramRendszer", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/TartalyElectro")]
        public async Task PostTartalyElectroAsync(bool value)
        {
            diagnoses.TartalyElectro = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("AramTartaly", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/KocsiElectro")]
        public async Task PostKocsiElectroAsync(bool value)
        {
            diagnoses.KocsiElectro = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("AramKocsi", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/KommKozElectroKomm")]
        public async Task PostKommKozElectroKommAsync(bool value)
        {
            diagnoses.KommKozElectroKomm = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("KommKommArammero", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/KocsiElectroKomm")]
        public async Task PostKocsiElectroKommAsync(bool value)
        {
            diagnoses.KocsiElectroKomm = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("KommTargoncaArammero", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/TartalyElectroKomm")]
        public async Task PostTartalyElectroKommAsync(bool value)
        {
            diagnoses.TartalyElectroKomm = value;
            await SaveDiagnosesToDB();
            DiagnoseData data = new DiagnoseData("KommTartalyArammero", value);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(data));
        }

        [HttpPost]
        [Route("ControlPanel/LED")]
        public async Task PostLEDAsync(string value)
        {
            diagnoses.LED = value;
            string carLedColor = "carLedColor" + "," + diagnoses.LED.ToLower();
            await SaveDiagnosesToDB();
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, carLedColor);

            string data = "start " + diagnoses.LED.ToLower();
            await mQTTPublisher.PublishTankLed(MQTTPublisher.mqttClient, data);
        }
        [HttpPost]
        [Route("MQTTConnection")]
        public async Task MQTT()
        {
            await mQTTPublisher.Publish();
        }

        [HttpPost("MQTTConnectionLost")]
        public async Task<bool> MQTTLost()
        {
            try
            {
                await mQTTPublisher.Subscribe();
                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }

        [HttpGet]
        [Route("DigitalFactory/CarLocation")]
        public string GetCarLocation()
        {
            return CarLocation;
        }

        [HttpGet]
        [Route("ControlPanel/GetDiagnosesFromDB")]
        public async Task<Diagnoses> GetDiagnosesFromDD()
        {
            var data = db.diagnoses.FirstOrDefault();
            Diagnoses diagnosesFromDB = new Diagnoses
            {
                CarSpeed = data.CarSpeed,
                System = data.System,
                Pause = data.Pause,
                WakeUp = data.WakeUp,
                PLCFailure = data.PLCFailure,
                RFID = data.RFID,
                RendszerElectro = data.RendszerElectro,
                TartalyElectro = data.TartalyElectro,
                KocsiElectro = data.KocsiElectro,
                KocsiElectroKomm = data.KocsiElectroKomm,
                TartalyElectroKomm = data.TartalyElectroKomm,
                KommKozElectroKomm = data.KommKozElectroKomm,
                CommunicationPowerError = data.CommunicationPowerError,
                CarError = data.CarError,
                ContainerEmpty = data.ContainerEmpty,
                LED = data.LED
            };

            diagnoses = diagnosesFromDB;
            
            string message = "carSpeed" + "," + diagnoses.CarSpeed;
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, message);

            DiagnoseData messageObj = new DiagnoseData("KommRendszer", diagnoses.System);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            message = "Paused" + "," + diagnoses.Pause;
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, message);

            message = "WakeUp" + "," + diagnoses.WakeUp;
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, message);

            messageObj = new DiagnoseData("GyarSzalagSzenz", diagnoses.PLCFailure);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("AramKommKozpont", diagnoses.CommunicationPowerError);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("AramKocsi", diagnoses.CarError);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("AramTartaly", diagnoses.ContainerEmpty);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("GyarRfidOlv", diagnoses.RFID);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("AramRendszer", diagnoses.RendszerElectro);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("GyarTartalySzenz", diagnoses.TartalyElectro);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("GyarTargoncaSzenz", diagnoses.KocsiElectro);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("KommKommArammero", diagnoses.KommKozElectroKomm);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("KommTargoncaArammero", diagnoses.KocsiElectroKomm);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            messageObj = new DiagnoseData("KommTartalyArammero", diagnoses.TartalyElectroKomm);
            await mQTTPublisher.PublishMessageAsync(MQTTPublisher.mqttClient, JsonConvert.SerializeObject(messageObj));

            message = "carLedColor" + "," + diagnoses.LED.ToLower();
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, message);

            message = "start " + diagnoses.LED.ToLower();
            await mQTTPublisher.PublishTankLed(MQTTPublisher.mqttClient, message);
            
            return diagnosesFromDB;
        }

        [HttpPost]
        [Route("ControlPanel/StopCar")]
        public async Task PostStopCarAsync(bool value)
        {
            string pause = "Paused" + "," + value;
            Console.WriteLine(pause);
            await mQTTPublisher.PublishToCarAsync(MQTTPublisher.mqttClient, pause);
        }
        /*
        // GET api/<FactoryController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<FactoryController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
            
        }

        // PUT api/<FactoryController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<FactoryController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
        */
    }
}
