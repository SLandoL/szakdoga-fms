using System.Collections.Generic;
using System.Linq;

namespace DiagnoseDashboard.Data
{
    /// <summary>
    /// Performs the root cause analysis on the in-memory FaultData list.
    ///
    /// The previous implementation selected root faults mainly by priority. That made it hard
    /// to express explicit cause-effect relationships between faults. This class keeps the
    /// hierarchy in code for now, so the RCA rules can be improved without changing the
    /// database schema or running a risky migration in the first development step.
    /// </summary>
    public class RootCauseAnalyzer
    {
        private readonly Dictionary<string, string> parentMap = new Dictionary<string, string>
        {
            // Top-level communication and power faults
            { "KommRendszer", null },
            { "AramRendszer", null },

            // Communication centre level
            { "KommKozpont", "KommRendszer" },
            { "KommKozpontUp", "KommRendszer" },
            { "AramKommKozpont", "AramRendszer" },

            // Device-level communication branches
            { "KommKocsi", "KommKozpont" },
            { "KommTartaly", "KommKozpont" },
            { "KommRfidUp", "KommKozpont" },

            // RFID business fault is treated as a consequence of RFID communication failure
            // when both are active. If only this fault is active, it remains a root cause.
            { "GyarRfidOlv", "KommRfidUp" },

            // Car branch
            { "KommKocsiEsp", "KommKocsi" },
            { "GyarTargoncaSzenz", "KommKocsi" },
            { "KommTargoncaArammero", "KommKocsi" },
            { "AramKocsi", "KommKocsi" },

            // Tank branch
            { "GyarSzalagSzenz", "KommTartaly" },
            { "GyarTartalySzenz", "KommTartaly" },
            { "AramTartaly", "KommTartaly" },
            { "KommTartalyArammero", "KommTartaly" },

            // Communication centre measuring device
            { "KommKommArammero", "KommKozpont" }
        };

        public void ResetRootFaults(List<FaultData> faults)
        {
            if (faults == null)
            {
                return;
            }

            foreach (FaultData fault in faults.Where(f => f.FaultStatus == FaultStatus.ROOTFAULT))
            {
                fault.FaultStatus = FaultStatus.FAULT;
            }
        }

        public void PropagateFaults(List<FaultData> faults)
        {
            if (faults == null)
            {
                return;
            }

            Dictionary<string, FaultData> lookup = BuildLookup(faults);

            foreach (FaultData activeFault in lookup.Values
                .Where(IsActiveFault)
                .OrderByDescending(f => f.Priority)
                .ThenBy(f => f.Name))
            {
                PropagateFrom(activeFault.Name, lookup, new HashSet<string>());
            }
        }

        public List<FaultData> DetectRootCauses(List<FaultData> faults)
        {
            if (faults == null)
            {
                return new List<FaultData>();
            }

            Dictionary<string, FaultData> lookup = BuildLookup(faults);
            List<FaultData> rootCauses = faults
                .Where(IsActiveFault)
                .Where(fault => !HasActiveAncestor(fault, lookup))
                .OrderByDescending(fault => fault.Priority)
                .ThenBy(fault => fault.Name)
                .ToList();

            foreach (FaultData rootCause in rootCauses)
            {
                rootCause.FaultStatus = FaultStatus.ROOTFAULT;
            }

            return rootCauses;
        }

        private Dictionary<string, FaultData> BuildLookup(List<FaultData> faults)
        {
            return faults
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .GroupBy(f => f.Name)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private bool IsActiveFault(FaultData fault)
        {
            return fault.FaultStatus == FaultStatus.FAULT || fault.FaultStatus == FaultStatus.ROOTFAULT;
        }

        private void PropagateFrom(string parentName, Dictionary<string, FaultData> lookup, HashSet<string> visited)
        {
            if (!visited.Add(parentName))
            {
                return;
            }

            foreach (string childName in GetDirectChildren(parentName))
            {
                if (!lookup.TryGetValue(childName, out FaultData child))
                {
                    continue;
                }

                if (child.FaultStatus == FaultStatus.WORKING)
                {
                    child.FaultStatus = FaultStatus.FAULT;
                }

                PropagateFrom(childName, lookup, visited);
            }
        }

        private IEnumerable<string> GetDirectChildren(string parentName)
        {
            return parentMap
                .Where(item => item.Value == parentName)
                .Select(item => item.Key);
        }

        private bool HasActiveAncestor(FaultData fault, Dictionary<string, FaultData> lookup)
        {
            string currentName = fault.Name;
            HashSet<string> visited = new HashSet<string>();

            while (parentMap.TryGetValue(currentName, out string parentName) && !string.IsNullOrEmpty(parentName))
            {
                if (!visited.Add(parentName))
                {
                    return false;
                }

                if (lookup.TryGetValue(parentName, out FaultData parent) && IsActiveFault(parent))
                {
                    return true;
                }

                currentName = parentName;
            }

            return false;
        }
    }
}
