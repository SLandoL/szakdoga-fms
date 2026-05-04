using FactoryService.Controllers;
using FactoryService.DataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactoryService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            MQTTPublisher mQTTPublisher = new MQTTPublisher();
            try
            {
                await mQTTPublisher.Publish();
                await mQTTPublisher.SubscribeCarLocation();
            }
            catch (MQTTnet.Exceptions.MqttCommunicationTimedOutException)
            {
                Console.WriteLine("Nem fut a Mqtt szerver");
            }

            CreateHostBuilder(args).Build().Run();
            
            await mQTTPublisher.Publish();
            await mQTTPublisher.SubscribeCarLocation();
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DiagnoseContext>();
                db.Database.Migrate();
            }

            host.Run();

        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("https://0.0.0.0:5002", "http://0.0.0.0:5003");
                    webBuilder.UseStartup<Startup>();
                    //webBuilder.UseUrls("https://172.20.0.235:44326");
                });
    }
}
