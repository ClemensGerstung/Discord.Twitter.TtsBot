using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.ResponseCompression;
using System.Linq;

namespace Discord.Twitter.TtsBot
{
  public class Startup
  {
    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddGrpc();
      services.AddResponseCompression(opts =>
      {
        opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            new[] { "application/octet-stream" });
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      app.UseResponseCompression();

      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
        app.UseWebAssemblyDebugging();
      }

      app.UseStaticFiles();
      app.UseBlazorFrameworkFiles();

      app.UseRouting();

      app.UseGrpcWeb();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapGrpcService<Impl>().EnableGrpcWeb();
        endpoints.MapFallbackToFile("index.html");
      });
    }
  }


  public class Program
  {
    public static void Main(string[] args)
    {
      CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
              webBuilder.UseStartup<Startup>();
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
