using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.Twitter.TtsBot.AdminAccess;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WebAccess.ViewModels;
using WebAccess.Models;
using System.Threading;

namespace WebAccess
{
  public class Program
  {
    public static async Task Main(string[] args)
    {
      GrpcChannel channel = null;
      CancellationTokenSource cts = new CancellationTokenSource();

      var builder = WebAssemblyHostBuilder.CreateDefault(args);
      builder.RootComponents.Add<App>("app");

      builder.Services.AddSingleton<IDialogService, DialogServiceImplementation>();
      builder.Services.AddSingleton<IDialogServiceExt>(services => services.GetService<IDialogService>() as DialogServiceImplementation);

      builder.Services.AddSingleton<IWebSocketHandler, ClientWebSocketHandler>();

      builder.Services.AddSingleton(services =>
      {
        var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());

        channel = GrpcChannel.ForAddress("http://localhost:50080/", new GrpcChannelOptions { HttpHandler = httpHandler });

        return new AdminAccess.AdminAccessClient(channel);
      });

      builder.Services.AddTransient<IMainViewModel, MainViewModel>();
      builder.Services.AddTransient<CreateUserViewModel>();
      builder.Services.AddTransient<UsersListViewModel>();
      builder.Services.AddTransient<QueueViewModel>();
      builder.Services.AddTransient<ItemsViewModel>();

      

      await using WebAssemblyHost host = builder.Build();
      
      _ = (host.Services.GetService<IWebSocketHandler>() as ClientWebSocketHandler).RunAsync(cts.Token);
      await host.RunAsync();
      await channel?.ShutdownAsync();

      cts.Cancel();
    }
  }
}
