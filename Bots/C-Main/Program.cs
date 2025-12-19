using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MainbotCSharp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<Bot>();
                })
                .UseConsoleLifetime();

            var host = builder.Build();

            var bot = host.Services.GetRequiredService<Bot>();
            await bot.InitializeAsync();

            await host.RunAsync();
        }
    }
}
