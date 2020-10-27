using Discord.Twitter.TtsBot.AdminAccess;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Tweetinvi.Core.Extensions;
using WebAccess.Models;
using WebAccess.ViewModels.Base;

namespace WebAccess.ViewModels
{
  public class QueueViewModel : ViewModelBase
  {
    private AdminAccess.AdminAccessClient _client;
    private IWebSocketHandler _webSocketHandler;

    public ObservableCollection<QueueItem> QueueItems { get; }

    public QueueViewModel(AdminAccess.AdminAccessClient client, IWebSocketHandler webSocketHandler)
    {
      _client = client;
      _webSocketHandler = webSocketHandler;
      QueueItems = new ObservableCollection<QueueItem>();

      webSocketHandler.QueueChanged += OnItemsChanged;
    }

    private void OnItemsChanged(object sender, Discord.Twitter.TtsBot.ItemsChangedEventArgs args)
    {
      if (args.NewItem != null) QueueItems.Add(args.NewItem);
      if (args.OldItem != null) QueueItems.Remove(args.OldItem);
    }

    public override async Task InitializeAsync()
    {
      await base.InitializeAsync();

      var response = await _client.GetQueueAsync(new GetQueueRequest());
      response.Items.ForEach(QueueItems.Add);
    }

    protected override void Dispose(bool disposing)
    {
      _webSocketHandler.QueueChanged -= OnItemsChanged;

      base.Dispose(disposing);
    }
  }
}
