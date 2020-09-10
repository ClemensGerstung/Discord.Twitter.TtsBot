using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using Newtonsoft.Json;
using System.IO;
using Microsoft.AspNetCore.Cors.Infrastructure;
using GrpcClient = Discord.Twitter.TtsBot.AdminAccess.AdminAccess.AdminAccessClient;
using Microsoft.Extensions.Configuration;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebSockets;
using SNWS = System.Net.WebSockets;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

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
      services.AddGrpcClient<GrpcClient>(OnAddGrpcClient);

      services.AddWebSockets(options => { });

      services.AddDbContext<DatabaseContext>();

      services.AddSingleton(JsonConvert.DeserializeObject<Option>(File.ReadAllText("config.json")));
      services.AddSingleton<DataStore>();
      services.AddSingleton<TtsBot>();
      

      services.AddCors(OnAddCors);

      void OnAddGrpcClient(GrpcClientFactoryOptions options) => options.Address = new Uri("http://localhost:50080/");

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

      app.Use(async (context, next) =>
      {
        if (context.Request.Path == "/connect")
        {
          if (context.WebSockets.IsWebSocketRequest)
          {
            var ttsBot = context.RequestServices.GetService<TtsBot>();
            SNWS.WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var socketFinishedTcs = new TaskCompletionSource<object>();

            ttsBot.AddWebSocket(webSocket, socketFinishedTcs);

            await socketFinishedTcs.Task;
          }
          else
          {
            context.Response.StatusCode = 400;
          }
        }
        else
        {
          await next();
        }
      });

      void OnUseEndPoints(IEndpointRouteBuilder endpoints)
      {
        endpoints.MapGrpcService<Impl>()
                 .EnableGrpcWeb()
                 .RequireCors("AllowAll");
      }
    }
  }
}
