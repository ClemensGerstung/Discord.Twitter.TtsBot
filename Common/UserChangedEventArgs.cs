using System;
using Discord.Twitter.TtsBot.AdminAccess;

namespace Discord.Twitter.TtsBot
{
  public class UserChangedEventArgs : EventArgs
  {
    public TwitterUser NewUser { get; }

    public TwitterUser OldUser { get; }

    public UserChangedEventArgs(TwitterUser newUser, TwitterUser oldUser)
    {
      NewUser = newUser;
      OldUser = oldUser;
    }

    public UserChangedEventArgs(UserChangedNotification notification)
      : this(notification.NewUser, notification.OldUser)
    {

    }
  }

  public class ItemsChangedEventArgs : EventArgs
  {
    public QueueItem NewItem { get; }

    public QueueItem OldItem { get; }

    public ItemsChangedEventArgs(QueueItem newItem, QueueItem oldItem)
    {
      NewItem = newItem;
      OldItem = oldItem;
    }

    public ItemsChangedEventArgs(QueueChangedNotification notification)
      : this(notification.NewItem, notification.OldItem)
    {

    }
  }

  public class ItemPlayedEventArgs : EventArgs
  {
    public QueueItem Item { get; }

    public ItemPlayedEventArgs(QueueItem item)
    {
      Item = item;
    }

    public ItemPlayedEventArgs(ItemPlayedNotification notification)
      : this(notification.Item)
    {

    }
  }
}
