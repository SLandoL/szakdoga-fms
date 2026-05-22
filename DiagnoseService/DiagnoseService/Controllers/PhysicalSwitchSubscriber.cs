using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Text;
using System.Threading.Tasks;

namespace DiagnoseService.Controllers
{
    public static class PhysicalSwitchSubscriber
    {
        private static readonly string mqttIP = "192.168.0.100";
        private static readonly MqttFactory mqttFactory = new MqttFactory();
        private static readonly IMqttClient mqttClient = mqttFactory.CreateMqttClient();
        private static bool handlersRegistered = false;

        public static async Task Subscribe()
        {
            if (mqttClient.IsConnected)
            {
                return;
            }

            var options = new MqttClientOptionsBuilder()
                            .WithClientId("physical-switch-subscriber-" + Guid.NewGuid())
                            .WithTcpServer(mqttIP, 1883)
                            .WithCleanSession()
                            .Build();

            if (!handlersRegistered)
            {
                mqttClient.UseConnectedHandler(async e =>
                {
                    Console.WriteLine("Connected to physical switch topics");
                    foreach (string topic in PhysicalSwitchManager.Topics)
                    {
                        var topicFilter = new TopicFilterBuilder().WithTopic(topic).Build();
                        await mqttClient.SubscribeAsync(topicFilter);
                    }
                });

                mqttClient.UseApplicationMessageReceivedHandler(e =>
                {
                    string topic = e.ApplicationMessage.Topic;
                    string payloadString = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    Console.WriteLine($"Physical switch MQTT message arrived. Topic: {topic}, Payload: {payloadString}");

                    if (PhysicalSwitchManager.TryHandleMessage(topic, payloadString, MQTTSubscriber.mqttClientPublish))
                    {
                        MQTTSubscriber.RefreshRfidDiagnose();
                        PhysicalSwitchManager.ApplyDiagnoses();
                    }
                });

                handlersRegistered = true;
            }

            await mqttClient.ConnectAsync(options);
        }
    }
}
