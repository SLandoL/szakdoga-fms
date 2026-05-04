using DiagnoseService.Model;
using Microsoft.AspNetCore.Http;
using DiagnoseService.Data;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace DiagnoseService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnoseController : ControllerBase
    {
        MQTTSubscriber mQTTSubscriber = new MQTTSubscriber();
        public static bool Failure = false;
        // GET: api/<DiagnoseController>
        [HttpGet]
        [Route("Dashboard/GetDiagnoses")]
        public Diagnoses Get()
        {
            return MQTTSubscriber.diagnose;
        }

        // GET api/<DiagnoseController>/5
        [HttpPost("MQTTConnection")]
        public async Task MQTT()
        {
            await mQTTSubscriber.Subscribe();
        }

        [HttpPost("MQTTConnectionLost")]
        public async Task<bool> MQTTLost()
        {
            try
            {
                if (MQTTSubscriber.mqttClientPublish.IsConnected)
                {
                    return true;
                }
                await mQTTSubscriber.Publish();
                await mQTTSubscriber.Subscribe();
                await mQTTSubscriber.SubscribeCarState();
                await mQTTSubscriber.SubscribeTankState();
                await mQTTSubscriber.SubscribeBottle();
                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }

        [HttpGet]
        [Route("Dashboard/GetMqttStatus")]
        public bool GetMqttStatus()
        {
            return MQTTSubscriber.mqttClientPublish.IsConnected;
        }

        [HttpGet]
        [Route("Dashboard/GetCarState")]
        public string GetCarState()
        {
            string cartemp = MQTTSubscriber.carState;
            MQTTSubscriber.carState = "OFFLINE";
            return cartemp;
        }
        [HttpGet]
        [Route("Dashboard/GetTankState")]
        public string GetTankState()
        {
            string tanktemp = MQTTSubscriber.tankState;
            MQTTSubscriber.tankState = "OFFLINE";
            return tanktemp;
        }
        [HttpGet]
        [Route("Dashboard/GetBottlesState")]
        public string GetBottleState()
        {
            string bottletemp = MQTTSubscriber.bottleState;
            MQTTSubscriber.bottleState = "OFFLINE";
            return bottletemp;
        }
        [HttpPost("IfFailure")]
        public async Task LEDChange(bool value)
        {
            Failure = value;
            await mQTTSubscriber.PublishMessageAsync(MQTTSubscriber.mqttClientPublish);
        }

        [HttpGet]
        [Route("Dashboard/MQTTIsConnected")]
        public bool GetMqttConnection()
        {
            return MQTTSubscriber.mqttClientPublish.IsConnected;
        }
    }
}
