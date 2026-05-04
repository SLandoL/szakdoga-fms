using DiagnoseService.Controllers;
using DiagnoseService.DataAccess;
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

namespace DiagnoseService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            MQTTSubscriber mQTTSubscriber = new MQTTSubscriber();
            try
            {
                await mQTTSubscriber.Publish();
                await mQTTSubscriber.Subscribe();
                await mQTTSubscriber.SubscribeCarState();
                await mQTTSubscriber.SubscribeTankState();
                await mQTTSubscriber.SubscribeBottle();
            }
            catch (MQTTnet.Exceptions.MqttCommunicationTimedOutException)
            {
                Console.WriteLine("Nem fut a Mqtt szerver.");
            }

            CreateHostBuilder(args).Build().Run();

            await mQTTSubscriber.Publish();
            await mQTTSubscriber.Subscribe();
            await mQTTSubscriber.SubscribeCarState();
            await mQTTSubscriber.SubscribeTankState();
            await mQTTSubscriber.SubscribeBottle();
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FailureContext>();
                db.Database.Migrate();
            }

            host.Run();

        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("https://0.0.0.0:5004", "http://0.0.0.0:5005");
                    webBuilder.UseStartup<Startup>();
                });
    }
}
