using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.Twitter.TtsBot.AdminAccess;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WebAccess.ViewModels;

namespace WebAccess
{
  public class Program
  {
    public static async Task Main(string[] args)
    {
      GrpcChannel channel = null;

      var builder = WebAssemblyHostBuilder.CreateDefault(args);
      builder.RootComponents.Add<App>("app");

      builder.Services.AddSingleton(services =>
      {
        var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());

        channel = GrpcChannel.ForAddress("http://localhost:50080/", new GrpcChannelOptions { HttpHandler = httpHandler });
        
        return new AdminAccess.AdminAccessClient(channel);
      });

      builder.Services.AddTransient<IMainViewModel, MainViewModel>();
      builder.Services.AddSingleton<CreateUserViewModel>();
      builder.Services.AddTransient<UsersListViewModel>();

      await builder.Build().RunAsync();
      await channel?.ShutdownAsync();
    }
  }
}
