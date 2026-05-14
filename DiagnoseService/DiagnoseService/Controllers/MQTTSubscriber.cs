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
using Newtonsoft.Json.Linq;
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
        private static readonly TimeSpan rfidHeartbeatTimeout = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan rfidReaderStatusTimeout = TimeSpan.FromSeconds(6);

        private static readonly RfidStatus rfidStatus = new RfidStatus();

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

        public static RfidStatus GetRfidStatusSnapshot()
        {
            UpdateRfidDiagnose();
            return new RfidStatus
            {
                DeviceId = rfidStatus.DeviceId,
                EspOnline = rfidStatus.EspOnline,
                TankReaderOk = rfidStatus.TankReaderOk,
                WarehouseReaderOk = rfidStatus.WarehouseReaderOk,
                TankReaderFresh = rfidStatus.TankReaderFresh,
                WarehouseReaderFresh = rfidStatus.WarehouseReaderFresh,
                CargoMatch = rfidStatus.CargoMatch,
                CargoMatchKnown = rfidStatus.CargoMatchKnown,
                TankCargoId = rfidStatus.TankCargoId,
                WarehouseCargoId = rfidStatus.WarehouseCargoId,
                TankReaderErrorCode = rfidStatus.TankReaderErrorCode,
                WarehouseReaderErrorCode = rfidStatus.WarehouseReaderErrorCode,
                LastHeartbeatUtc = rfidStatus.LastHeartbeatUtc,
                LastTankReaderStatusUtc = rfidStatus.LastTankReaderStatusUtc,
                LastWarehouseReaderStatusUtc = rfidStatus.LastWarehouseReaderStatusUtc,
                LastCargoReadUtc = rfidStatus.LastCargoReadUtc,
                DiagnosticSummary = rfidStatus.DiagnosticSummary
            };
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
                await SubscribeTopic(mqttClient, "Diagnoses");

                // Legacy RFID topics kept for backward compatibility with the original firmware.
                await SubscribeTopic(mqttClient, "Tank_olvaso_mukodik");
                await SubscribeTopic(mqttClient, "WH_olvaso_mukodik");
                await SubscribeTopic(mqttClient, "TANK_rakomany");
                await SubscribeTopic(mqttClient, "WH_rakomany");
                await SubscribeTopic(mqttClient, "Rakomany_egyezes");

                // Structured RFID diagnostics topics introduced in the third development phase.
                await SubscribeTopic(mqttClient, "RFID/Heartbeat");
                await SubscribeTopic(mqttClient, "RFID/TankReader/Status");
                await SubscribeTopic(mqttClient, "RFID/WarehouseReader/Status");
                await SubscribeTopic(mqttClient, "RFID/TankReader/Cargo");
                await SubscribeTopic(mqttClient, "RFID/WarehouseReader/Cargo");
                await SubscribeTopic(mqttClient, "RFID/CargoMatch");

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
                else if (TryHandleStructuredRfidMessage(topic, payloadString))
                {
                    UpdateRfidDiagnose();
                }
                else if (topic == "Tank_olvaso_mukodik")
                {
                    if (bool.TryParse(payloadString, out bool value))
                    {
                        UpdateReaderState("tank", value, 0);
                        UpdateRfidDiagnose();
                    }
                }
                else if (topic == "WH_olvaso_mukodik")
                {
                    if (bool.TryParse(payloadString, out bool value))
                    {
                        UpdateReaderState("warehouse", value, 0);
                        UpdateRfidDiagnose();
                    }
                }
                else if (topic == "TANK_rakomany")
                {
                    rfidStatus.TankCargoId = payloadString.Trim('\0', ' ', '\r', '\n');
                    rfidStatus.LastCargoReadUtc = DateTime.UtcNow;
                    UpdateRfidDiagnose();
                }
                else if (topic == "WH_rakomany")
                {
                    rfidStatus.WarehouseCargoId = payloadString.Trim('\0', ' ', '\r', '\n');
                    rfidStatus.LastCargoReadUtc = DateTime.UtcNow;
                    UpdateRfidDiagnose();
                }
                else if (topic == "Rakomany_egyezes")
                {
                    if (bool.TryParse(payloadString, out bool value))
                    {
                        rfidStatus.CargoMatch = value;
                        rfidStatus.CargoMatchKnown = true;
                        rfidStatus.LastCargoReadUtc = DateTime.UtcNow;
                        UpdateRfidDiagnose();
                    }
                }
            });

            await mqttClient.ConnectAsync(options);
        }

        private static async Task SubscribeTopic(IMqttClient mqttClient, string topic)
        {
            var topicFilter = new TopicFilterBuilder().WithTopic(topic).Build();
            await mqttClient.SubscribeAsync(topicFilter);
        }

        private static bool TryHandleStructuredRfidMessage(string topic, string payloadString)
        {
            if (!topic.StartsWith("RFID/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                JObject payload = JObject.Parse(payloadString);

                if (topic == "RFID/Heartbeat")
                {
                    rfidStatus.DeviceId = ReadString(payload, "deviceId", rfidStatus.DeviceId);
                    rfidStatus.LastHeartbeatUtc = DateTime.UtcNow;

                    if (payload["tankReaderOk"] != null)
                    {
                        rfidStatus.TankReaderOk = payload.Value<bool>("tankReaderOk");
                    }

                    if (payload["warehouseReaderOk"] != null)
                    {
                        rfidStatus.WarehouseReaderOk = payload.Value<bool>("warehouseReaderOk");
                    }

                    return true;
                }

                if (topic == "RFID/TankReader/Status")
                {
                    UpdateReaderState("tank", payload.Value<bool>("ok"), payload.Value<int?>("errorCode") ?? 0);
                    return true;
                }

                if (topic == "RFID/WarehouseReader/Status")
                {
                    UpdateReaderState("warehouse", payload.Value<bool>("ok"), payload.Value<int?>("errorCode") ?? 0);
                    return true;
                }

                if (topic == "RFID/TankReader/Cargo")
                {
                    rfidStatus.TankCargoId = ReadString(payload, "cargoId", rfidStatus.TankCargoId);
                    rfidStatus.LastCargoReadUtc = DateTime.UtcNow;
                    return true;
                }

                if (topic == "RFID/WarehouseReader/Cargo")
                {
                    rfidStatus.WarehouseCargoId = ReadString(payload, "cargoId", rfidStatus.WarehouseCargoId);
                    rfidStatus.LastCargoReadUtc = DateTime.UtcNow;
                    return true;
                }

                if (topic == "RFID/CargoMatch")
                {
                    rfidStatus.CargoMatch = payload.Value<bool>("match");
                    rfidStatus.CargoMatchKnown = true;
                    rfidStatus.TankCargoId = ReadString(payload, "tankCargoId", rfidStatus.TankCargoId);
                    rfidStatus.WarehouseCargoId = ReadString(payload, "warehouseCargoId", rfidStatus.WarehouseCargoId);
                    rfidStatus.LastCargoReadUtc = DateTime.UtcNow;
                    return true;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"RFID JSON feldolgozási hiba. Topic: {topic}, Error: {ex.Message}");
                return true;
            }

            return false;
        }

        private static string ReadString(JObject payload, string propertyName, string fallback)
        {
            JToken token = payload[propertyName];
            if (token == null)
            {
                return fallback;
            }

            string value = token.Value<string>();
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static void UpdateReaderState(string reader, bool ok, int errorCode)
        {
            if (string.Equals(reader, "tank", StringComparison.OrdinalIgnoreCase))
            {
                rfidStatus.TankReaderOk = ok;
                rfidStatus.TankReaderErrorCode = errorCode;
                rfidStatus.LastTankReaderStatusUtc = DateTime.UtcNow;
            }
            else
            {
                rfidStatus.WarehouseReaderOk = ok;
                rfidStatus.WarehouseReaderErrorCode = errorCode;
                rfidStatus.LastWarehouseReaderStatusUtc = DateTime.UtcNow;
            }
        }

        private static MqttFactory mqttFactoryCar = new MqttFactory();
        private static IMqttClient mqttClientCar = mqttFactoryCar.CreateMqttClient();

        private static void UpdateRfidDiagnose()
        {
            DateTime now = DateTime.UtcNow;

            rfidStatus.EspOnline = rfidStatus.LastHeartbeatUtc != DateTime.MinValue && now - rfidStatus.LastHeartbeatUtc <= rfidHeartbeatTimeout;
            rfidStatus.TankReaderFresh = rfidStatus.LastTankReaderStatusUtc != DateTime.MinValue && now - rfidStatus.LastTankReaderStatusUtc <= rfidReaderStatusTimeout;
            rfidStatus.WarehouseReaderFresh = rfidStatus.LastWarehouseReaderStatusUtc != DateTime.MinValue && now - rfidStatus.LastWarehouseReaderStatusUtc <= rfidReaderStatusTimeout;

            bool readersOk =
                rfidStatus.EspOnline &&
                rfidStatus.TankReaderFresh &&
                rfidStatus.WarehouseReaderFresh &&
                rfidStatus.TankReaderOk &&
                rfidStatus.WarehouseReaderOk;

            diagnose.KommRfidUp.Data = !readersOk;
            diagnose.GyarRfidOlv.Data = readersOk && rfidStatus.CargoMatchKnown && !rfidStatus.CargoMatch;

            rfidStatus.DiagnosticSummary = BuildRfidDiagnosticSummary(readersOk);

            Console.WriteLine($"RFID diagnózis frissítve. EspOnline: {rfidStatus.EspOnline}, TankFresh: {rfidStatus.TankReaderFresh}, WHFresh: {rfidStatus.WarehouseReaderFresh}, TankOk: {rfidStatus.TankReaderOk}, WHOk: {rfidStatus.WarehouseReaderOk}, CargoKnown: {rfidStatus.CargoMatchKnown}, CargoMatch: {rfidStatus.CargoMatch}, KommHiba: {!readersOk}, GyárHiba: {readersOk && rfidStatus.CargoMatchKnown && !rfidStatus.CargoMatch}");
        }

        private static string BuildRfidDiagnosticSummary(bool readersOk)
        {
            if (!rfidStatus.EspOnline)
            {
                return "RFID ESP offline or heartbeat timeout.";
            }

            if (!rfidStatus.TankReaderFresh)
            {
                return "Tank RFID reader status is stale.";
            }

            if (!rfidStatus.WarehouseReaderFresh)
            {
                return "Warehouse RFID reader status is stale.";
            }

            if (!rfidStatus.TankReaderOk)
            {
                return "Tank RFID reader reports a fault.";
            }

            if (!rfidStatus.WarehouseReaderOk)
            {
                return "Warehouse RFID reader reports a fault.";
            }

            if (!rfidStatus.CargoMatchKnown)
            {
                return "RFID readers are available, but no cargo match result has been received yet.";
            }

            if (!rfidStatus.CargoMatch)
            {
                return "RFID readers are available, but the cargo identifiers do not match.";
            }

            return "RFID ESP and both readers are online, and the cargo identifiers match.";
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
