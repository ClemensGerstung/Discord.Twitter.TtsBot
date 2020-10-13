using System;
using SNWS = System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Discord.Twitter.TtsBot
{
  using Discord.Twitter.TtsBot.AdminAccess;
  using Google.Protobuf;

  public class WebSocketHandler
  {
    private IDictionary<SNWS.WebSocket, TaskCompletionSource<object>> _webSockets;

    public WebSocketHandler(DataStore store)
    {
      _webSockets = new Dictionary<SNWS.WebSocket, TaskCompletionSource<object>>();

      store.ItemPlayed += OnItemPlayed;
      store.ItemsChanged += OnItemsChanged;
      store.QueueChanged += OnQueueChanged;
      store.UsersChanged += OnUsersChanged;
    }

    private async void OnUsersChanged(object sender, UserChangedEventArgs args)
    {
      UserChangedNotification notification = new UserChangedNotification();
      notification.Type = NotificationType.UserChanged;
      notification.NewUser = args.NewUser;
      notification.OldUser = args.OldUser;

      await BroadcastAsync(notification);
    }

    private async void OnQueueChanged(object sender, ItemsChangedEventArgs e)
    {
      QueueChangedNotification notification = new QueueChangedNotification();
      notification.NewItem = e.NewItem;
      notification.OldItem = e.OldItem;
      notification.Type = NotificationType.QueueChanged;

      await BroadcastAsync(notification);
    }

    private async void OnItemsChanged(object sender, ItemsChangedEventArgs e)
    {
      QueueChangedNotification notification = new QueueChangedNotification();
      notification.NewItem = e.NewItem;
      notification.OldItem = e.OldItem;
      notification.Type = NotificationType.ItemsChanged;

      await BroadcastAsync(notification);
    }

    private async void OnItemPlayed(object sender, ItemPlayedEventArgs e)
    {
      ItemPlayedNotification notification = new ItemPlayedNotification();
      notification.Type = NotificationType.ItemPlayed;
      notification.Item = e.Item;

      await BroadcastAsync(notification);
    }

    public async Task OnUse(HttpContext context, Func<Task> next)
    {
      if (context.Request.Path == "/connect")
      {
        if (context.WebSockets.IsWebSocketRequest)
        {
          SNWS.WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
          
          var socketFinishedTcs = new TaskCompletionSource<object>();

          _webSockets.Add(webSocket, socketFinishedTcs);

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
    }

    public async Task BroadcastAsync(IMessage message)
    {
      using MemoryStream stream = new MemoryStream();

      message.WriteTo(stream);
      stream.Seek(0, SeekOrigin.Begin);

      ReadOnlyMemory<byte> readOnlyMemory = new ReadOnlyMemory<byte>();
      await stream.WriteAsync(readOnlyMemory);

      bool res = stream.TryGetBuffer(out ArraySegment<byte> array);

      foreach (var kvp in _webSockets.ToArray())
      {
        var webSocket = kvp.Key;
        if (webSocket.CloseStatus.HasValue)
        {
          await webSocket.CloseAsync(SNWS.WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
          _webSockets.Remove(webSocket);
          kvp.Value.SetResult(new object());
          continue;
        }

        await webSocket.SendAsync(array, SNWS.WebSocketMessageType.Binary, true, CancellationToken.None);
      }
    }
  }
}
