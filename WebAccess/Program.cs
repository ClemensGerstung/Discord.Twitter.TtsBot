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
  public interface IDialogService
  {
    Task<bool?> OpenDialog();

    object State { get; }
  }

  public interface IDialogServiceExt
  {
    event EventHandler DialogOpen;
    event EventHandler DialogClose;

    void CloseDialog(bool? result);

    object State { get; set; }
  }

  class DialogServiceImplementation : IDialogService,
                                      IDialogServiceExt
  {
    private TaskCompletionSource<bool?> _tcs;

    object IDialogService.State => State;

    public object State { get; set; }

    public event EventHandler DialogOpen;
    public event EventHandler DialogClose;

    public void CloseDialog(bool? result)
    {
      if (_tcs.Task.IsCompleted) return;
      _tcs.SetResult(result);
      DialogClose?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool?> OpenDialog()
    {
      _tcs = new TaskCompletionSource<bool?>();
      State = null;
      DialogOpen?.Invoke(this, EventArgs.Empty);
      return _tcs.Task;
    }
  }

  public class Program
  {
    public static async Task Main(string[] args)
    {
      GrpcChannel channel = null;

      var builder = WebAssemblyHostBuilder.CreateDefault(args);
      builder.RootComponents.Add<App>("app");

      builder.Services.AddSingleton<IDialogService, DialogServiceImplementation>();
      builder.Services.AddSingleton<IDialogServiceExt>(services => services.GetService<IDialogService>() as DialogServiceImplementation);
      builder.Services.AddSingleton(services =>
      {
        var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());

        channel = GrpcChannel.ForAddress("http://localhost:50080/", new GrpcChannelOptions { HttpHandler = httpHandler });

        return new AdminAccess.AdminAccessClient(channel);
      });

      builder.Services.AddTransient<IMainViewModel, MainViewModel>();
      builder.Services.AddTransient<CreateUserViewModel>();
      builder.Services.AddTransient<UsersListViewModel>();

      await builder.Build().RunAsync();
      await channel?.ShutdownAsync();
    }
  }
}
