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
      services.AddSingleton(new DataStore());
      services.AddSingleton(AddTtsBot);

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

      TtsBot AddTtsBot(IServiceProvider serviceProvider)
      {
        var grpcClient = serviceProvider.GetService<GrpcClient>();
        var dataStore = serviceProvider.GetService<DataStore>();

        Option option = JsonConvert.DeserializeObject<Option>(File.ReadAllText("config.json"));
        return new TtsBot(option, grpcClient, dataStore);
      }
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      app.UseRouting();
      app.UseGrpcWeb();
      app.UseCors();

      app.UseEndpoints(OnUseEndPoints);

      void OnUseEndPoints(IEndpointRouteBuilder endpoints)
      {
        endpoints.MapGrpcService<Impl>()
                 .EnableGrpcWeb()
                 .RequireCors("AllowAll");
      }
    }
  }
}
