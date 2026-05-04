using DiagnoseService.Model;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FactoryService.Controllers
{
    public class MQTTPublisher
    {
        private static string mqttIP = "192.168.0.100";
        private static MqttFactory mqttFactory = new MqttFactory();
        public static IMqttClient mqttClient = mqttFactory.CreateMqttClient();
        public async Task Publish()
        {
            var options = new MqttClientOptionsBuilder()
                            .WithClientId(Guid.NewGuid().ToString())
                            .WithTcpServer(mqttIP, 1883)
                            .WithCleanSession()
                            .Build();
            mqttClient.UseConnectedHandler(e =>
            {
                Console.WriteLine("Connected as publisher");
            });
            await mqttClient.ConnectAsync(options);

            //await PublishMessageAsync(mqttClient);
        }

        public async Task PublishMessageAsync(IMqttClient mqttClient, string data)
        {
            var diagnose = FactoryController.diagnoses;
            var message = new MqttApplicationMessageBuilder()
                            .WithTopic("Diagnoses")
                            .WithPayload(data)
                            .WithAtLeastOnceQoS()
                            .Build();
            if (mqttClient.IsConnected)
            {
                await mqttClient.PublishAsync(message);
            }
        }

        public async Task PublishToCarAsync(IMqttClient mqttClient, string data)
        {
            var message = new MqttApplicationMessageBuilder()
                            .WithTopic("carManagement")
                            .WithPayload(data)
                            .WithAtLeastOnceQoS()
                            .Build();
            if (mqttClient.IsConnected)
            {
                await mqttClient.PublishAsync(message);
            }
        }

        public async Task Subscribe()
        {
            var mqttFactory = new MqttFactory();
            IMqttClient mqttClient = mqttFactory.CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                            .WithClientId(Guid.NewGuid().ToString())
                            .WithTcpServer(mqttIP, 1883)
                            .WithCleanSession()
                            .Build();
            Console.WriteLine("Connected");
            await mqttClient.ConnectAsync(options);
        }

        public async Task PublishTankLed(IMqttClient mqttClient, string data)
        {
            var message = new MqttApplicationMessageBuilder()
                            .WithTopic("tank-esp")
                            .WithPayload(data)
                            .WithAtLeastOnceQoS()
                            .Build();
            if (mqttClient.IsConnected)
            {
                await mqttClient.PublishAsync(message);
            }
        }

        private static MqttFactory mqttCar = new MqttFactory();
        private static IMqttClient mqttClientCar = mqttCar.CreateMqttClient();


        public async Task SubscribeCarLocation()
        {
            var options = new MqttClientOptionsBuilder()
                            .WithClientId(Guid.NewGuid().ToString())
                            .WithTcpServer(mqttIP, 1883)
                            .WithCleanSession()
                            .Build();
            mqttClientCar.UseConnectedHandler(async e =>
            {
                Console.WriteLine("Connected to State for car location");
                var topicFilter = new TopicFilterBuilder()
                                    .WithTopic("CarLocation")
                                    .Build();
                await mqttClientCar.SubscribeAsync(topicFilter);
            });
            mqttClientCar.UseApplicationMessageReceivedHandler(e =>
            {
                FactoryController.CarLocation = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            });
            await mqttClientCar.ConnectAsync(options);
        }
    }
}
