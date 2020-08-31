using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.ResponseCompression;
using System.Linq;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.Tasks;
using System;

namespace Discord.Twitter.TtsBot
{
  public class Startup
  {
    public void ConfigureServices(IServiceCollection services)
    {
      var builder = services.AddGrpc();
      services.AddCors(o => o.AddPolicy("AllowAll", builder =>
      {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader()
               .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
      }));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      app.UseRouting();
      app.UseGrpcWeb();
      app.UseCors();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapGrpcService<Impl>()
                 .EnableGrpcWeb()
                 .RequireCors("AllowAll");
      });
      
    }
  }


  public class Program
  {
    public static async Task Main(string[] args)
    {
      using (IHost host = CreateHostBuilder(args).Build())
      {
        await host.StartAsync();

        

        Console.ReadLine();

        await host.StopAsync();
      }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
              webBuilder.UseStartup<Startup>();
              webBuilder.UseUrls("http://*:50080");
              webBuilder.UseUrls("https://*:50443");
            });


    //static async Task Main(string[] args)
    //{
    //  //if (args.Length != 1)
    //  //{
    //  //  return;
    //  //}

    //  //Option option = JsonConvert.DeserializeObject<Option>(File.ReadAllText(args[0]));

    //  //TtsBot bot = new TtsBot(option);


    //  Server server = new Server
    //  {
    //    Services = { AdminAccess.AdminAccess.BindService(new Impl()) },
    //    Ports = { new ServerPort("localhost", 50001, ServerCredentials.Insecure) }
    //  };

    //  server.Start();

    //  Console.WriteLine("Greeter server listening on port " + 50001);
    //  Console.WriteLine("Press any key to stop the server...");
    //  Console.ReadLine();

    //  await server.ShutdownAsync();
    //}

    //static async Task Main(string[] args)
    //{
    //  if(args.Length != 1)
    //  {
    //    return;
    //  }

    //  Option option = JsonConvert.DeserializeObject<Option>(File.ReadAllText(args[0]));

    //  TtsBot bot = new TtsBot(option);
    //  await bot.RunAsync();
    //}
  }
}
