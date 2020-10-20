using Discord.Twitter.TtsBot.AdminAccess;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Tweetinvi.Core.Extensions;
using WebAccess.ViewModels.Base;

namespace WebAccess.ViewModels
{
  public class QueueViewModel : ViewModelBase
  {
    private AdminAccess.AdminAccessClient _client;

    public ObservableCollection<QueueItem> QueueItems { get; }

    public QueueViewModel(AdminAccess.AdminAccessClient client)
    {
      _client = client;
      QueueItems = new ObservableCollection<QueueItem>();
    }

    public override async Task InitializeAsync()
    {
      await base.InitializeAsync();

      var response = await _client.GetQueueAsync(new GetQueueRequest());
      response.Items.ForEach(QueueItems.Add);
    }
  }
}
