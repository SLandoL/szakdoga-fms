using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnoseDashboard.Data
{
    public class FaultData
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public FaultStatus FaultStatus { get; set; } = FaultStatus.WORKING;
        public FaultPriority Priority { get; set; }
        public bool Valid { get; set; }
        public int ParentId { get; set; }

        public FaultData() { }
        public FaultData(FaultPriority p, String n)
        {
            Priority = p;
            Name = n;
        }
    }
}
