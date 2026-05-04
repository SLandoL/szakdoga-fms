using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactoryService.Model
{
    public class DiagnoseData
    {
        public string Name { get; set; }

        public bool Data { get; set; } = false;
        public DiagnoseData(string name, bool data= false)
        {
            Name = name;
            Data = data;
        }
    }
}
