using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using Newtonsoft.Json;
using System.IO;
using Microsoft.AspNetCore.Cors.Infrastructure;
using GrpcClient = Discord.Twitter.TtsBot.AdminAccess.AdminAccess.AdminAccessClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebSockets;
using System.Net.Http;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;

namespace Discord.Twitter.TtsBot
{
  public class Startup
  {
    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
      services.AddGrpc();
      //services.AddGrpcClient<GrpcClient>(OnAddGrpcClient);

      services.AddWebSockets(options => { });

      services.AddDbContext<DatabaseContext>();

      services.AddSingleton(_ =>
      {
        var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());
        var channel = GrpcChannel.ForAddress("http://localhost:50080/", new GrpcChannelOptions { HttpHandler = httpHandler });
        return new GrpcClient(channel);
      });
      services.AddSingleton(JsonConvert.DeserializeObject<Option>(File.ReadAllText("config.json")));
      services.AddSingleton<DataStore>();
      services.AddSingleton<TtsBot>();
      services.AddSingleton<WebSocketHandler>();
      

      services.AddCors(OnAddCors);

      //void OnAddGrpcClient(GrpcClientFactoryOptions options) 
      //{
      //  options.Address = new Uri("http://localhost:50080/");
      //}

      void OnAddCors(CorsOptions options) => options.AddPolicy("AllowAll", 
                                                               OnAddPolicy);

      void OnAddPolicy(CorsPolicyBuilder builder)
      {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader()
               .WithExposedHeaders("Grpc-Status", 
                                   "Grpc-Message", 
                                   "Grpc-Encoding", 
                                   "Grpc-Accept-Encoding");
      }
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      app.UseRouting();
      app.UseGrpcWeb();
      app.UseCors();
    
      var webSocketOptions = new WebSocketOptions()
      {
        KeepAliveInterval = TimeSpan.FromSeconds(120),
        ReceiveBufferSize = 4 * 1024
      };

      app.UseWebSockets(webSocketOptions);
      app.UseEndpoints(OnUseEndPoints);

      app.Use(app.ApplicationServices.GetService<WebSocketHandler>().OnUse);
      
      void OnUseEndPoints(IEndpointRouteBuilder endpoints)
      {
        endpoints.MapGrpcService<Impl>()
                 .EnableGrpcWeb()
                 .RequireCors("AllowAll");
      }
    }
  }
}
