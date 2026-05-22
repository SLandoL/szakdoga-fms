using DiagnoseService.Data;
using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiagnoseService.Controllers
{
    public static class PhysicalSwitchManager
    {
        private const string TankPowerSwitchId = "TankPower";
        private const string RfidTankPowerSwitchId = "RfidTankPower";
        private const string RfidWarehousePowerSwitchId = "RfidWarehousePower";
        private const string ExtraStop1SwitchId = "ExtraStop1";
        private const string ExtraStop2SwitchId = "ExtraStop2";
        private const string DebugSwitchId = "Debug";
        private const string ForceNextStopFactoryEvent = "ForceNextStopFactory";

        private static readonly Dictionary<string, PhysicalSwitchStatus> physicalSwitches = CreatePhysicalSwitchDefaults();

        public static IReadOnlyCollection<string> Topics => physicalSwitches.Values.Select(item => item.Topic).ToList();

        public static bool TryHandleMessage(string topic, string payloadString, IMqttClient publisher)
        {
            PhysicalSwitchStatus switchStatus = physicalSwitches.Values.FirstOrDefault(item => string.Equals(item.Topic, topic, StringComparison.OrdinalIgnoreCase));
            if (switchStatus == null)
            {
                return false;
            }

            if (!TryParseSwitchPayload(payloadString, switchStatus, out bool state, out bool changed, out string eventName))
            {
                return true;
            }

            UpdatePhysicalSwitchState(switchStatus, state, changed, eventName);
            ApplyDiagnoses();

            if (switchStatus.SwitchId == DebugSwitchId && switchStatus.Changed)
            {
                _ = PublishAgvForceNextStopFactoryAsync(publisher, switchStatus.State);
            }

            return true;
        }

        public static void ApplyDiagnoses()
        {
            if (IsSwitchKnownAndDisabled(TankPowerSwitchId))
            {
                MQTTSubscriber.diagnose.AramTartaly.Data = true;
            }
            else if (IsSwitchKnownAndEnabled(TankPowerSwitchId))
            {
                MQTTSubscriber.diagnose.AramTartaly.Data = false;
            }

            if (IsSwitchKnownAndDisabled(RfidTankPowerSwitchId) || IsSwitchKnownAndDisabled(RfidWarehousePowerSwitchId))
            {
                // The switch layer may raise an RFID communication fault, but it must not clear
                // a fault that was measured by heartbeat, reader timeout or reader status logic.
                MQTTSubscriber.diagnose.KommRfidUp.Data = true;
            }
        }

        public static PhysicalSwitchSnapshot GetSnapshot()
        {
            ApplyDiagnoses();
            List<PhysicalSwitchStatus> switches = physicalSwitches
                .Values
                .OrderBy(item => item.Category)
                .ThenBy(item => item.SwitchId)
                .Select(item => item.Clone())
                .ToList();

            return new PhysicalSwitchSnapshot
            {
                Switches = switches,
                Summary = BuildPhysicalSwitchSummary(switches)
            };
        }

        public static bool HasRfidPowerInterruption()
        {
            return IsSwitchKnownAndDisabled(RfidTankPowerSwitchId) || IsSwitchKnownAndDisabled(RfidWarehousePowerSwitchId);
        }

        public static string BuildRfidPowerSummary()
        {
            bool tankPowerDisabled = IsSwitchKnownAndDisabled(RfidTankPowerSwitchId);
            bool warehousePowerDisabled = IsSwitchKnownAndDisabled(RfidWarehousePowerSwitchId);

            if (tankPowerDisabled && warehousePowerDisabled)
            {
                return "Both RFID reader power switches are disabled; this is diagnosed as an RFID communication fault.";
            }

            if (tankPowerDisabled)
            {
                return "Tank RFID reader power switch is disabled; this is diagnosed as an RFID communication fault.";
            }

            if (warehousePowerDisabled)
            {
                return "Warehouse RFID reader power switch is disabled; this is diagnosed as an RFID communication fault.";
            }

            return string.Empty;
        }

        private static bool TryParseSwitchPayload(string payloadString, PhysicalSwitchStatus switchStatus, out bool state, out bool changed, out string eventName)
        {
            state = switchStatus.State;
            changed = false;
            eventName = string.Empty;

            if (bool.TryParse(payloadString, out bool simpleBool))
            {
                state = simpleBool;
                changed = !switchStatus.HasValue || switchStatus.State != state;
                return true;
            }

            try
            {
                JObject payload = JObject.Parse(payloadString);
                bool? stateFromPayload = payload.Value<bool?>("state") ?? payload.Value<bool?>("enabled");
                if (!stateFromPayload.HasValue)
                {
                    return false;
                }

                state = stateFromPayload.Value;
                changed = payload.Value<bool?>("changed") ?? (!switchStatus.HasValue || switchStatus.State != state);
                eventName = ReadString(payload, "event", string.Empty);
                return true;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Physical switch JSON feldolgozási hiba. Topic: {switchStatus.Topic}, Error: {ex.Message}");
                return false;
            }
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

        private static void UpdatePhysicalSwitchState(PhysicalSwitchStatus switchStatus, bool state, bool changed, string eventName)
        {
            DateTime now = DateTime.UtcNow;
            bool computedChanged = !switchStatus.HasValue || switchStatus.State != state;

            switchStatus.State = state;
            switchStatus.Changed = changed || computedChanged;
            switchStatus.EventName = string.IsNullOrWhiteSpace(eventName) ? DefaultSwitchEvent(switchStatus.SwitchId) : eventName;
            switchStatus.HasValue = true;
            switchStatus.LastUpdateUtc = now;

            if (switchStatus.Changed)
            {
                switchStatus.LastChangedUtc = now;
            }

            Console.WriteLine($"Physical switch updated. Id: {switchStatus.SwitchId}, State: {switchStatus.State}, Changed: {switchStatus.Changed}, Event: {switchStatus.EventName}");
        }

        private static string DefaultSwitchEvent(string switchId)
        {
            return switchId == DebugSwitchId ? ForceNextStopFactoryEvent : string.Empty;
        }

        private static bool IsSwitchKnownAndDisabled(string switchId)
        {
            return physicalSwitches.TryGetValue(switchId, out PhysicalSwitchStatus switchStatus) &&
                   switchStatus.HasValue &&
                   !switchStatus.State;
        }

        private static bool IsSwitchKnownAndEnabled(string switchId)
        {
            return physicalSwitches.TryGetValue(switchId, out PhysicalSwitchStatus switchStatus) &&
                   switchStatus.HasValue &&
                   switchStatus.State;
        }

        private static async Task PublishAgvForceNextStopFactoryAsync(IMqttClient publisher, bool debugState)
        {
            if (publisher == null || !publisher.IsConnected)
            {
                Console.WriteLine("Debug switch event received, but MQTT publisher is not connected. Command was not published.");
                return;
            }

            string payload = $"{{\"event\":\"{ForceNextStopFactoryEvent}\",\"nextStop\":\"Factory\",\"source\":\"DebugSwitch\",\"debugState\":{debugState.ToString().ToLowerInvariant()},\"timestampUtc\":\"{DateTime.UtcNow:o}\"}}";

            var message = new MqttApplicationMessageBuilder()
                            .WithTopic("Agv/Command/ForceNextStop")
                            .WithPayload(payload)
                            .WithAtLeastOnceQoS()
                            .Build();

            await publisher.PublishAsync(message);
            Console.WriteLine("Debug switch event published: next AGV stop forced to Factory.");
        }

        private static string BuildPhysicalSwitchSummary(List<PhysicalSwitchStatus> switches)
        {
            if (!switches.Any(item => item.HasValue))
            {
                return "No physical switch state has been received yet.";
            }

            List<string> activeNotes = new List<string>();

            if (IsSwitchKnownAndDisabled(TankPowerSwitchId)) activeNotes.Add("tank power is disabled");
            if (IsSwitchKnownAndDisabled(RfidTankPowerSwitchId)) activeNotes.Add("tank RFID reader power is disabled");
            if (IsSwitchKnownAndDisabled(RfidWarehousePowerSwitchId)) activeNotes.Add("warehouse RFID reader power is disabled");
            if (IsSwitchKnownAndEnabled(ExtraStop1SwitchId)) activeNotes.Add("extra stop 1 is enabled");
            if (IsSwitchKnownAndEnabled(ExtraStop2SwitchId)) activeNotes.Add("extra stop 2 is enabled");

            PhysicalSwitchStatus debugSwitch = physicalSwitches[DebugSwitchId];
            if (debugSwitch.HasValue && debugSwitch.Changed)
            {
                activeNotes.Add("debug switch changed and requested Factory as the next AGV stop");
            }

            return activeNotes.Any()
                ? "Physical switch state: " + string.Join(", ", activeNotes) + "."
                : "Physical switch state received; no active diagnostic power interruption is selected.";
        }

        private static Dictionary<string, PhysicalSwitchStatus> CreatePhysicalSwitchDefaults()
        {
            return new Dictionary<string, PhysicalSwitchStatus>
            {
                [TankPowerSwitchId] = new PhysicalSwitchStatus
                {
                    SwitchId = TankPowerSwitchId,
                    Topic = "PhysicalSwitch/TankPower",
                    Category = "DiagnosticPower",
                    DiagnosticMeaning = "Tartály tápellátási hiba / AramTartaly",
                    ControlMeaning = "state=true: táp engedélyezve; state=false: táp megszakítva"
                },
                [RfidTankPowerSwitchId] = new PhysicalSwitchStatus
                {
                    SwitchId = RfidTankPowerSwitchId,
                    Topic = "PhysicalSwitch/RfidTankPower",
                    Category = "DiagnosticPower",
                    DiagnosticMeaning = "Tank oldali RFID olvasó tápmegszakítása / KommRfidUp",
                    ControlMeaning = "state=true: táp engedélyezve; state=false: táp megszakítva"
                },
                [RfidWarehousePowerSwitchId] = new PhysicalSwitchStatus
                {
                    SwitchId = RfidWarehousePowerSwitchId,
                    Topic = "PhysicalSwitch/RfidWarehousePower",
                    Category = "DiagnosticPower",
                    DiagnosticMeaning = "Raktár oldali RFID olvasó tápmegszakítása / KommRfidUp",
                    ControlMeaning = "state=true: táp engedélyezve; state=false: táp megszakítva"
                },
                [ExtraStop1SwitchId] = new PhysicalSwitchStatus
                {
                    SwitchId = ExtraStop1SwitchId,
                    Topic = "Agv/Switch/ExtraStop1",
                    Category = "AgvRoute",
                    DiagnosticMeaning = "Nem diagnosztikai hiba",
                    ControlMeaning = "enabled=true: extra 1. megálló engedélyezve"
                },
                [ExtraStop2SwitchId] = new PhysicalSwitchStatus
                {
                    SwitchId = ExtraStop2SwitchId,
                    Topic = "Agv/Switch/ExtraStop2",
                    Category = "AgvRoute",
                    DiagnosticMeaning = "Nem diagnosztikai hiba",
                    ControlMeaning = "enabled=true: extra 2. megálló engedélyezve"
                },
                [DebugSwitchId] = new PhysicalSwitchStatus
                {
                    SwitchId = DebugSwitchId,
                    Topic = "Agv/Switch/Debug",
                    Category = "AgvDebug",
                    DiagnosticMeaning = "Nem diagnosztikai hiba",
                    ControlMeaning = "Állapotváltozáskor ForceNextStopFactory esemény"
                }
            };
        }
    }
}
