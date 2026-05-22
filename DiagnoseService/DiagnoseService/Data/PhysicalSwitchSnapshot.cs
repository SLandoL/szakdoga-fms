using System.Collections.Generic;
using System.Linq;

namespace DiagnoseService.Data
{
    public class PhysicalSwitchSnapshot
    {
        public List<PhysicalSwitchStatus> Switches { get; set; } = new List<PhysicalSwitchStatus>();
        public string Summary { get; set; } = "No physical switch state has been received yet.";

        public PhysicalSwitchStatus GetSwitch(string switchId)
        {
            return Switches.FirstOrDefault(item => item.SwitchId == switchId);
        }
    }
}
