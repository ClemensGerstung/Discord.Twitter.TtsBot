using Discord.Twitter.TtsBot.AdminAccess;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweetinvi.Core.Extensions;
using WebAccess.Models;
using WebAccess.ViewModels.Base;

namespace WebAccess.ViewModels
{
  public class UsersListViewModel : ViewModelBase
  {
    private AdminAccess.AdminAccessClient _client;
    private IDialogService _dialogService;

    public ObservableCollection<TwitterUser> Users { get; set; }

    public ICommand AddUserCommand { get; }

    public UsersListViewModel(AdminAccess.AdminAccessClient client, IDialogService dialogService)
    {
      _client = client;
      _dialogService = dialogService;
      Users = new ObservableCollection<TwitterUser>();
      AddUserCommand = new AsyncCommand(OnCommandExecute);
    }

    public override async Task InitializeAsync()
    {
      var response = await _client.GetAllTwitterUsersAsync(new GetAllTwitterUserRequest());
      response.Users.ForEach(Users.Add);
    }

    internal async Task OnCommandExecute()
    {
      var result = await _dialogService.OpenDialog();
      NewUserData state = _dialogService.State as NewUserData;
      
      if(result.GetValueOrDefault(false) && 
        state != null)
      {
        var response = await _client.AddTwitterUserAsync(new GetTwitterUserRequest { Handle = state.Handle, Language = state.Handle, VoiceName = state.VoiceName });
      }
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        // TODO: dispose managed state (managed objects)
        Users.Clear();
        Users = null;
      }

      // TODO: free unmanaged resources (unmanaged objects) and override finalizer
      // TODO: set large fields to null
      base.Dispose(disposing);
    }
  }
}
