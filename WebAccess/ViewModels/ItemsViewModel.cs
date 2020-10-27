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
  public class ItemsViewModel : ViewModelBase
  {
    private AdminAccess.AdminAccessClient _client;
    private IWebSocketHandler _webSocketHandler;

    public ObservableCollection<QueueItem> Items { get; }

    public ItemsViewModel(AdminAccess.AdminAccessClient client, IWebSocketHandler webSocketHandler)
    {
      _client = client;
      _webSocketHandler = webSocketHandler;
      Items = new ObservableCollection<QueueItem>();

      webSocketHandler.ItemsChanged += OnItemsChanged;
    }

    private void OnItemsChanged(object sender, Discord.Twitter.TtsBot.ItemsChangedEventArgs args)
    {
      if (args.NewItem != null) Items.Add(args.NewItem);
      if (args.OldItem != null) Items.Remove(args.OldItem);
    }

    public override async Task InitializeAsync()
    {
      await base.InitializeAsync();

      var response = await _client.GetItemsAsync(new GetQueueRequest());
      response.Items.ForEach(Items.Add);
    }

    protected override void Dispose(bool disposing)
    {
      _webSocketHandler.ItemsChanged -= OnItemsChanged;

      base.Dispose(disposing);
    }
  }
}
