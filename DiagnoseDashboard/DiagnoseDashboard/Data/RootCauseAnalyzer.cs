using System;
using System.Collections.Generic;
using System.Linq;

namespace DiagnoseDashboard.Data
{
    /// <summary>
    /// Performs root cause analysis and consequence propagation on the in-memory FaultData list.
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

        public void ResetAnalysisStatuses(List<FaultData> faults)
        {
            if (faults == null)
            {
                return;
            }

            foreach (FaultData fault in faults)
            {
                if (fault.FaultStatus == FaultStatus.ROOTFAULT)
                {
                    fault.FaultStatus = FaultStatus.FAULT;
                }
                else if (fault.FaultStatus == FaultStatus.CONSEQUENCE)
                {
                    fault.FaultStatus = FaultStatus.WORKING;
                }
            }
        }

        [Obsolete("Use ResetAnalysisStatuses instead. It resets both previous ROOTFAULT and CONSEQUENCE analysis markers.")]
        public void ResetRootFaults(List<FaultData> faults)
        {
            ResetAnalysisStatuses(faults);
        }

        public void PropagateFaults(List<FaultData> faults)
        {
            if (faults == null)
            {
                return;
            }

            Dictionary<string, FaultData> lookup = BuildLookup(faults);
            List<FaultData> measuredFaults = lookup.Values
                .Where(IsMeasuredFault)
                .OrderByDescending(f => f.Priority)
                .ThenBy(f => f.Name)
                .ToList();

            foreach (FaultData measuredFault in measuredFaults)
            {
                MarkConsequences(measuredFault.Name, lookup, new HashSet<string>());
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
                .Where(IsMeasuredFault)
                .Where(fault => !HasMeasuredAncestor(fault, lookup))
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

        private bool IsMeasuredFault(FaultData fault)
        {
            return fault.FaultStatus == FaultStatus.FAULT || fault.FaultStatus == FaultStatus.ROOTFAULT;
        }

        private void MarkConsequences(string parentName, Dictionary<string, FaultData> lookup, HashSet<string> visited)
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

                // Only inferred, not measured, children are marked as consequences.
                // Real FAULT and ROOTFAULT values must not be overwritten.
                if (child.FaultStatus == FaultStatus.WORKING || child.FaultStatus == FaultStatus.CONSEQUENCE)
                {
                    child.FaultStatus = FaultStatus.CONSEQUENCE;
                    MarkConsequences(childName, lookup, visited);
                }
                else if (IsMeasuredFault(child))
                {
                    MarkConsequences(childName, lookup, visited);
                }
            }
        }

        private IEnumerable<string> GetDirectChildren(string parentName)
        {
            return parentMap
                .Where(item => item.Value == parentName)
                .Select(item => item.Key);
        }

        private bool HasMeasuredAncestor(FaultData fault, Dictionary<string, FaultData> lookup)
        {
            string currentName = fault.Name;
            HashSet<string> visited = new HashSet<string>();

            while (parentMap.TryGetValue(currentName, out string? parentName) && !string.IsNullOrEmpty(parentName))
            {
                if (!visited.Add(parentName))
                {
                    return false;
                }

                if (lookup.TryGetValue(parentName, out FaultData parent) && IsMeasuredFault(parent))
                {
                    return true;
                }

                currentName = parentName;
            }

            return false;
        }
    }
}
