using System;
using Discord.Twitter.TtsBot.AdminAccess;

namespace Discord.Twitter.TtsBot
{
  public class UserChangedEventArgs : EventArgs
  {
    public TwitterUser NewUser { get; }

    public TwitterUser OldUser { get; }

    internal UserChangedEventArgs(TwitterUser newUser, TwitterUser oldUser)
    {
      NewUser = newUser;
      OldUser = oldUser;
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
  }

  public class ItemPlayedEventArgs : EventArgs
  {
    public QueueItem Item { get; }

    public ItemPlayedEventArgs(QueueItem item)
    {
      Item = item;
    }
  }
}
