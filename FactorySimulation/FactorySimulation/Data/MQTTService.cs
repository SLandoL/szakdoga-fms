using FactorySimulation.DataAccess;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactorySimulation.Data
{
    public class MQTTService
    {
        private static string baseUrl = "https://localhost:5002/";
        private IFactoryData factoryData = RestService.For<IFactoryData>(baseUrl);
        private static bool connection;
        private static bool print;
        public ProfileMessages alert = ProfileMessages.NULL;
        public ProfileMessages answer = ProfileMessages.NULL;

        public async Task MQTTConnectionAsync()
        {
            await factoryData.ConnectMQTT();
        }

        public async Task<bool> MQTTConnectionLostAsync()
        {
            connection = await factoryData.GetLostConnection();
            Console.WriteLine("Connection: " + connection);
            return connection;
        }

        public async Task ConnectMQTT()
        {
            print = await MQTTConnectionLostAsync();
            Console.WriteLine(print);
            if (print == false)
            {
                try
                {
                    await MQTTConnectionAsync();
                }
                catch (Exception)
                {
                    Console.WriteLine("Nem lehetett csatlakozni jajajaj");
                    answer = ProfileMessages.MQTTNOTUP;
                    Console.WriteLine("ConnectMQTT No: " + answer);
                }
            }
            else
            {
                answer = ProfileMessages.MQTTISREACHABLE;
                Console.WriteLine("ConnectMQTT Yes: " + answer);
                try
                {
                    await MQTTConnectionAsync();
                }
                catch (Exception)
                {
                }
                
            }
            CheckConnection();
        }

        public void CheckConnection()
        {
            if (answer == ProfileMessages.MQTTNOTUP)
            {
                alert = ProfileMessages.MQTTNOTUP;
            }
            else if (answer == ProfileMessages.MQTTISREACHABLE)
            {
                alert = ProfileMessages.MQTTISREACHABLE;
            }
        }
    }
}
