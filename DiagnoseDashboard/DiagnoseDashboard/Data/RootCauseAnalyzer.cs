using System.Collections.Generic;
using System.Linq;

namespace DiagnoseDashboard.Data
{
    /// <summary>
    /// Performs root cause analysis on the in-memory FaultData list.
    ///
    /// The hierarchy is intentionally kept as an explicit code-level map in this development
    /// step. This avoids a database migration while still making the cause-effect relations
    /// between faults clear and reviewable.
    /// </summary>
    public class RootCauseAnalyzer
    {
        private readonly Dictionary<string, string?> parentMap = new Dictionary<string, string?>
        {
            // Top-level communication and power faults
            { "KommRendszer", null },
            { "AramRendszer", null },

            // Communication centre level
            { "KommKozpont", "KommRendszer" },
            { "KommKozpontUp", "KommKozpont" },
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
            // In the first RCA refactor this method deliberately does not change FaultStatus.
            // The project currently has only WORKING, FAULT and ROOTFAULT states. If propagated
            // consequences were also written as FAULT, the dashboard could no longer distinguish
            // real measured faults from inferred consequence faults.
            //
            // The explicit hierarchy is therefore used only by DetectRootCauses() to suppress
            // lower-level active faults when an active ancestor is already present. A later UI or
            // data-model refactor can add a separate CONSEQUENCE/DERIVED state if consequence
            // visualisation is needed.
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

        private bool HasActiveAncestor(FaultData fault, Dictionary<string, FaultData> lookup)
        {
            string currentName = fault.Name;
            HashSet<string> visited = new HashSet<string>();

            while (parentMap.TryGetValue(currentName, out string? parentName) && !string.IsNullOrEmpty(parentName))
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
