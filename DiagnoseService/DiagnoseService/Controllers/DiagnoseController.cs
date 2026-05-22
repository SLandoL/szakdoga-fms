using DiagnoseService.Data;
using DiagnoseService.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DiagnoseService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnoseController : ControllerBase
    {
        MQTTSubscriber mQTTSubscriber = new MQTTSubscriber();
        public static bool Failure = false;

        [HttpGet]
        [Route("Dashboard/GetDiagnoses")]
        public Diagnoses Get()
        {
            MQTTSubscriber.RefreshRfidDiagnose();
            PhysicalSwitchManager.ApplyDiagnoses();
            return MQTTSubscriber.diagnose;
        }

        [HttpPost("MQTTConnection")]
        public async Task MQTT()
        {
            await mQTTSubscriber.Subscribe();
            await PhysicalSwitchSubscriber.Subscribe();
        }

        [HttpPost("MQTTConnectionLost")]
        public async Task<bool> MQTTLost()
        {
            try
            {
                if (MQTTSubscriber.mqttClientPublish.IsConnected)
                {
                    await PhysicalSwitchSubscriber.Subscribe();
                    return true;
                }
                await mQTTSubscriber.Publish();
                await mQTTSubscriber.Subscribe();
                await mQTTSubscriber.SubscribeCarState();
                await mQTTSubscriber.SubscribeTankState();
                await mQTTSubscriber.SubscribeBottle();
                await PhysicalSwitchSubscriber.Subscribe();
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
            return MQTTSubscriber.GetCarStateSnapshot();
        }

        [HttpGet]
        [Route("Dashboard/GetTankState")]
        public string GetTankState()
        {
            return MQTTSubscriber.GetTankStateSnapshot();
        }

        [HttpGet]
        [Route("Dashboard/GetBottlesState")]
        public string GetBottleState()
        {
            return MQTTSubscriber.GetBottleStateSnapshot();
        }

        [HttpGet]
        [Route("Dashboard/GetRfidStatus")]
        public RfidStatus GetRfidStatus()
        {
            RfidStatus status = MQTTSubscriber.GetRfidStatusSnapshot();
            if (PhysicalSwitchManager.HasRfidPowerInterruption())
            {
                status.DiagnosticSummary = PhysicalSwitchManager.BuildRfidPowerSummary();
            }
            return status;
        }

        [HttpGet]
        [Route("Dashboard/GetPhysicalSwitchStatus")]
        public PhysicalSwitchSnapshot GetPhysicalSwitchStatus()
        {
            return PhysicalSwitchManager.GetSnapshot();
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
