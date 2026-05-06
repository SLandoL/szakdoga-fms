using DiagnoseService.Data;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Reflection;

namespace DiagnoseService.Controllers
{
    public class MQTTSubscriber
    {
        private static string mqttIP = "192.168.0.100";
        public static string carState = "OFFLINE";
        public static string tankState = "OFFLINE";
        public static string bottleState = "OFFLINE";
        public static Diagnoses diagnose = new Diagnoses();

        private static DateTime carStateLastUpdate = DateTime.MinValue;
        private static DateTime tankStateLastUpdate = DateTime.MinValue;
        private static DateTime bottleStateLastUpdate = DateTime.MinValue;
        private static readonly TimeSpan deviceStateTimeout = TimeSpan.FromSeconds(15);

        // Alapértelmezetten hamis, tehát induláskor "nem elérhető"-nek tekintjük
        private static bool tankReaderOk = false;
        private static bool whReaderOk = false;

        // JAVÍTÁS: Kezdetben igazra állítjuk, hogy ne legyen azonnal hiba a rakományegyezésnél,
        // amíg nem történt tényleges olvasás.
        private static bool rakomanyOk = true;

        private static MqttFactory mqttFactoryPublish = new MqttFactory();
        public static IMqttClient mqttClientPublish = mqttFactoryPublish.CreateMqttClient();

        public static string GetCarStateSnapshot()
        {
            return GetFreshStateOrOffline(carState, carStateLastUpdate);
        }

        public static string GetTankStateSnapshot()
        {
            return GetFreshStateOrOffline(tankState, tankStateLastUpdate);
        }

        public static string GetBottleStateSnapshot()
        {
            return GetFreshStateOrOffline(bottleState, bottleStateLastUpdate);
        }

        private static string GetFreshStateOrOffline(string state, DateTime lastUpdate)
        {
            if (lastUpdate == DateTime.MinValue)
            {
                return "OFFLINE";
            }

            if (DateTime.UtcNow - lastUpdate > deviceStateTimeout)
            {
                return "OFFLINE";
            }

            return state;
        }

        public async Task Publish()
        {
            var options = new MqttClientOptionsBuilder()
                            .WithClientId(Guid.NewGuid().ToString())
                            .WithTcpServer(mqttIP, 1883)
                            .WithCleanSession()
                            .Build();
            mqttClientPublish.UseConnectedHandler(e =>
            {
                Console.WriteLine("Connected as publisher");
            });
            await mqttClientPublish.ConnectAsync(options);
        }

        public async Task PublishMessageAsync(IMqttClient mqttClientPublish)
        {
            var message = new MqttApplicationMessageBuilder()
                            .WithTopic("tank-esp")
                            .WithPayload(DiagnoseController.Failure.ToString())
                            .WithAtLeastOnceQoS()
                            .Build();
            if (mqttClientPublish.IsConnected)
            {
                await mqttClientPublish.PublishAsync(message);
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

            mqttClient.UseConnectedHandler(async e =>
            {
                Console.WriteLine("Connected");
                var topicFilter = new TopicFilterBuilder().WithTopic("Diagnoses").Build();
                await mqttClient.SubscribeAsync(topicFilter);

                var tankReaderFilter = new TopicFilterBuilder().WithTopic("Tank_olvaso_mukodik").Build();
                await mqttClient.SubscribeAsync(tankReaderFilter);

                var whReaderFilter = new TopicFilterBuilder().WithTopic("WH_olvaso_mukodik").Build();
                await mqttClient.SubscribeAsync(whReaderFilter);

                var rakomanyEgyezesFilter = new TopicFilterBuilder().WithTopic("Rakomany_egyezes").Build();
                await mqttClient.SubscribeAsync(rakomanyEgyezesFilter);

                // Kapcsolódáskor az alapértelmezett értékek alapján még nincs olvasó-kommunikáció,
                // ezért a rendszer kommunikációs RFID hibát jelezhet. A rakományhiba csak akkor
                // lesz mért hiba, ha az olvasók működnek, de a rakomány nem egyezik.
                UpdateRfidDiagnose();
            });

            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payloadString = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                Console.WriteLine($"MQTT message arrived. Topic: {topic}, Payload: {payloadString}");

                if (topic == "Diagnoses")
                {
                    DiagnoseData diagnoseData = JsonConvert.DeserializeObject<DiagnoseData>(payloadString);
                    foreach (PropertyInfo prop in typeof(Diagnoses).GetProperties().Where(p => p.PropertyType == typeof(DiagnoseData)))
                    {
                        DiagnoseData currentDiagnose = (DiagnoseData)prop.GetValue(diagnose, null);
                        if (currentDiagnose.Name == diagnoseData.Name)
                        {
                            currentDiagnose.Data = diagnoseData.Data;
                            prop.SetValue(diagnose, currentDiagnose);
                        }
                    }
                }
                else if (topic == "Tank_olvaso_mukodik")
                {
                    if (bool.TryParse(payloadString, out bool value))
                    {
                        tankReaderOk = value;
                        UpdateRfidDiagnose();
                    }
                }
                else if (topic == "WH_olvaso_mukodik")
                {
                    if (bool.TryParse(payloadString, out bool value))
                    {
                        whReaderOk = value;
                        UpdateRfidDiagnose();
                    }
                }
                else if (topic == "Rakomany_egyezes")
                {
                    if (bool.TryParse(payloadString, out bool value))
                    {
                        rakomanyOk = value;
                        UpdateRfidDiagnose();
                    }
                }
            });

            await mqttClient.ConnectAsync(options);
        }

        private static MqttFactory mqttFactoryCar = new MqttFactory();
        private static IMqttClient mqttClientCar = mqttFactoryCar.CreateMqttClient();

        private static void UpdateRfidDiagnose()
        {
            // Ha mindkét olvasó True-t küldött, akkor az RFID kommunikáció rendben van.
            bool readersOk = tankReaderOk && whReaderOk;

            // A kommunikációs hiba mért hiba, ha legalább az egyik olvasó nem működik.
            diagnose.KommRfidUp.Data = !readersOk;

            // A gyártási / rakományegyezési hiba csak akkor mért hiba, ha az olvasók működnek,
            // de a rakomány nem egyezik. Ha az olvasók nem elérhetők, akkor a GyarRfidOlv
            // állapotát az RCA CONSEQUENCE-ként származtatja a KommRfidUp hibából.
            diagnose.GyarRfidOlv.Data = readersOk && !rakomanyOk;

            Console.WriteLine($"RFID diagnózis frissítve. Tank: {tankReaderOk}, WH: {whReaderOk}, Egyenlő: {rakomanyOk}, KommHiba: {!readersOk}, GyárHiba: {readersOk && !rakomanyOk}");
        }

        public async Task SubscribeCarState()
        {
            var options = new MqttClientOptionsBuilder()
                           .WithClientId(Guid.NewGuid().ToString())
                           .WithTcpServer(mqttIP, 1883)
                           .WithCleanSession()
                           .Build();
            mqttClientCar.UseConnectedHandler(async e =>
            {
                Console.WriteLine("Connected to State");
                var topicFilter = new TopicFilterBuilder()
                                    .WithTopic("MQTTState")
                                    .Build();
                await mqttClientCar.SubscribeAsync(topicFilter);
            });
            mqttClientCar.UseApplicationMessageReceivedHandler(e =>
            {
                carState = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                carStateLastUpdate = DateTime.UtcNow;
                Console.WriteLine($"Received diagnoses - " + carState);
            });
            await mqttClientCar.ConnectAsync(options);
        }

        private static MqttFactory mqttFactoryBottle = new MqttFactory();
        private static IMqttClient mqttClientBottle = mqttFactoryBottle.CreateMqttClient();


        public async Task SubscribeBottle()
        {
            var options = new MqttClientOptionsBuilder()
                           .WithClientId(Guid.NewGuid().ToString())
                           .WithTcpServer(mqttIP, 1883)
                           .WithCleanSession()
                           .Build();
            mqttClientBottle.UseConnectedHandler(async e =>
            {
                Console.WriteLine("Connected to State");
                var topicFilter = new TopicFilterBuilder()
                                    .WithTopic("caresp")
                                    .Build();
                await mqttClientBottle.SubscribeAsync(topicFilter);
            });
            mqttClientBottle.UseApplicationMessageReceivedHandler(e =>
            {
                bottleState = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                bottleStateLastUpdate = DateTime.UtcNow;
                Console.WriteLine($"Received Bottles are - " + bottleState);
            });
            await mqttClientBottle.ConnectAsync(options);
        }

        private static MqttFactory mqttFactoryTank = new MqttFactory();
        private static IMqttClient mqttClientTank = mqttFactoryTank.CreateMqttClient();
        public async Task SubscribeTankState()
        {
            var options = new MqttClientOptionsBuilder()
                           .WithClientId(Guid.NewGuid().ToString())
                           .WithTcpServer(mqttIP, 1883)
                           .WithCleanSession()
                           .Build();
            mqttClientTank.UseConnectedHandler(async e =>
            {
                Console.WriteLine("Subscribed to Tank ");
                var topicFilter = new TopicFilterBuilder()
                                    .WithTopic("tankesp")
                                    .Build();
                await mqttClientTank.SubscribeAsync(topicFilter);
            });
            mqttClientTank.UseApplicationMessageReceivedHandler(e =>
            {
                tankState = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                tankStateLastUpdate = DateTime.UtcNow;
                Console.WriteLine($"Received diagnoses - " + tankState);
            });
            await mqttClientTank.ConnectAsync(options);
        }
    }
}