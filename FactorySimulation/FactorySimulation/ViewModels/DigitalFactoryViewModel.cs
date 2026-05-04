using FactorySimulation.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace FactorySimulation.ViewModels
{
    public class DigitalFactoryViewModel
    {
        private DigitalFactoryService digitalFactoryService = new DigitalFactoryService();
        public string CarLocation { get; set; }
        public async Task GetCarLocation()
        {
            await digitalFactoryService.GetCarLocationAsync();
            CarLocation = digitalFactoryService.CarLocaton;
        }
    }
}
