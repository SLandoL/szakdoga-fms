using FactorySimulation.DataAccess;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactorySimulation.Data
{
    public class DigitalFactoryService
    {
        private static string baseUrl = "https://localhost:5002/";
        private IFactoryData factoryData = RestService.For<IFactoryData>(baseUrl);
        public string CarLocaton { get; set; }
        public async Task GetCarLocationAsync()
        {
            CarLocaton = await factoryData.GetCarLocation();
            Console.WriteLine(CarLocaton);
        }
    }
}
