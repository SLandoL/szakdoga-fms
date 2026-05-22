using System;

namespace DiagnoseDashboard.Model
{
    public class PhysicalSwitchStatus
    {
        public string SwitchId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DiagnosticMeaning { get; set; } = string.Empty;
        public string ControlMeaning { get; set; } = string.Empty;
        public bool HasValue { get; set; }
        public bool State { get; set; }
        public bool Changed { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime LastUpdateUtc { get; set; } = DateTime.MinValue;
        public DateTime LastChangedUtc { get; set; } = DateTime.MinValue;
    }
}
