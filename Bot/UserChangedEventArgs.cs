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

  public class ItemQueuedEventArgs : EventArgs
  {
    public QueueItem Item { get; }

    public ItemQueuedEventArgs(QueueItem item)
    {
      Item = item;
    }
  }
}
