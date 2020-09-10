using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System;
using System.Threading;
using Discord.Twitter.TtsBot.AdminAccess;

namespace Discord.Twitter.TtsBot
{

  public class Program
  {
    public static async Task Main(string[] args)
    {
      ManualResetEvent endSignal = new ManualResetEvent(false);
      Console.CancelKeyPress += (sender, args) => endSignal.Set();

      using IHost host = CreateHostBuilder(args).Build();
      TtsBot bot = host.Services.GetService<TtsBot>();

      await bot.StartAsync();
      await host.StartAsync();

      endSignal.WaitOne();

      await host.StopAsync();
      await bot.ShutdownAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
      IHostBuilder builder = Host.CreateDefaultBuilder(args)
                                 .ConfigureWebHostDefaults(webBuilder =>
                                 {
                                   webBuilder.UseStartup<Startup>();
                                   webBuilder.UseUrls("http://*:50080");
                                   //webBuilder.UseUrls("https://*:50443");
                                 });

      return builder;
    }
  }
}
