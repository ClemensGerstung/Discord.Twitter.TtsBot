using Discord.Twitter.TtsBot;
using Discord.Twitter.TtsBot.AdminAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebAccess.Models
{
  public interface IWebSocketHandler
  {
    event EventHandler<UserChangedEventArgs> UsersChanged;
    event EventHandler<ItemsChangedEventArgs> ItemsChanged;
    event EventHandler<ItemsChangedEventArgs> QueueChanged;
    event EventHandler<ItemPlayedEventArgs> ItemPlayed;
  }

  public class ClientWebSocketHandler : IWebSocketHandler,
                                        IDisposable
  {
    private ClientWebSocket _webSocket = new ClientWebSocket();
    private bool _disposedValue;

    public event EventHandler<UserChangedEventArgs> UsersChanged;
    public event EventHandler<ItemsChangedEventArgs> ItemsChanged;
    public event EventHandler<ItemsChangedEventArgs> QueueChanged;
    public event EventHandler<ItemPlayedEventArgs> ItemPlayed;

    protected virtual void Dispose(bool disposing)
    {
      if (!_disposedValue)
      {
        if (disposing)
        {
          _webSocket.Dispose();
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        _disposedValue = true;
      }
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
      var buffer = new ArraySegment<byte>(new byte[2048]);
      await _webSocket.ConnectAsync(new Uri("ws://localhost:50080/connect"), CancellationToken.None);

      while (!stoppingToken.IsCancellationRequested)
      {
        WebSocketReceiveResult received;
        using MemoryStream data = new MemoryStream();

        do
        {
          received = await _webSocket.ReceiveAsync(buffer, stoppingToken);
          Console.WriteLine("Received {0} b ({1})", received.Count, received.EndOfMessage);
          await data.WriteAsync(buffer.Slice(0, received.Count));
        } while (!received.EndOfMessage);

        var type = GetType(data);

        switch (type)
        {
          case NotificationType.UserChanged:
            {
              var userNotification = UserChangedNotification.Parser.ParseFrom(data);

              UsersChanged?.Invoke(this, new UserChangedEventArgs(userNotification));
              break;
            }

          case NotificationType.QueueChanged:
            {
              var notification = QueueChangedNotification.Parser.ParseFrom(data);

              QueueChanged?.Invoke(this, new ItemsChangedEventArgs(notification));
              break;
            }

          case NotificationType.ItemsChanged:
            {
              var notification = QueueChangedNotification.Parser.ParseFrom(data);

              ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(notification));
              break;
            }

          case NotificationType.ItemPlayed:
            {
              var notification = ItemPlayedNotification.Parser.ParseFrom(data);

              ItemPlayed?.Invoke(this, new ItemPlayedEventArgs(notification));
              break;
            }

          default:
            Console.WriteLine("Unknown Type \"{0}\"", type);
            break;
        }

      }

      await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);

      NotificationType GetType(MemoryStream data)
      {
        try
        {
          data.Seek(0, SeekOrigin.Begin);
          var item = NotificationItem.Parser.ParseFrom(data);
          Console.WriteLine("Parsed `NotificationItem`: {0}", item != null);

          data.Seek(0, SeekOrigin.Begin);

          return item.Type;
        }
        catch(Exception e)
        {
          return NotificationType.None;
        }
      }
    }

    public void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}
