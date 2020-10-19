using Discord.Twitter.TtsBot.AdminAccess;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweetinvi.Core.Extensions;

namespace WebAccess.ViewModels
{
  public class UsersListViewModel : IDisposable
  {
    private bool _disposedValue;
    private AdminAccess.AdminAccessClient _client;
    private CreateUserViewModel _createUserViewModel;

    public ObservableCollection<TwitterUser> Users { get; set; }

    public ICommand AddUserCommand { get; }

    public UsersListViewModel(AdminAccess.AdminAccessClient client, CreateUserViewModel createUserViewModel)
    {
      _client = client;
      _createUserViewModel = createUserViewModel;
      Users = new ObservableCollection<TwitterUser>();
      AddUserCommand = new AsyncCommand(OnCommandExecute);
    }

    internal async Task InitializeAsync()
    {
      var response = await _client.GetAllTwitterUsersAsync(new GetAllTwitterUserRequest());
      response.Users.ForEach(Users.Add);
    }

    internal async Task OnCommandExecute()
    {
      object res = await _createUserViewModel.OpenDialog();
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!_disposedValue)
      {
        if (disposing)
        {
          // TODO: dispose managed state (managed objects)
          Users.Clear();
          Users = null;
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        _disposedValue = true;
      }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~UsersListViewModel()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}
