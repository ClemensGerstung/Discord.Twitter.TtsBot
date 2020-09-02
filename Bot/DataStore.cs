using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Discord.Twitter.TtsBot.AdminAccess;

namespace Discord.Twitter.TtsBot
{
  public class DataStore
  {
    private readonly IDictionary<long, TwitterUser> _users = new ConcurrentDictionary<long, TwitterUser>();
    private readonly ConcurrentDictionary<long, QueueItem> _items = new ConcurrentDictionary<long, QueueItem>();
    private readonly ConcurrentQueue<long> _queue = new ConcurrentQueue<long>();

    public ICollection<TwitterUser> Users => _users.Values;

    public IList<QueueItem> QueueItems => _queue.Select(id => _items[id])
                                                .ToList();

    public ICollection<QueueItem> Items => _items.Values;

    public event EventHandler<UserAddedEventArgs> UserAdded;

    public void Enqueue(QueueItem item)
    {
      _queue.Enqueue(item.TweetId);
      _items.AddOrUpdate(item.TweetId, item, (id, old) => item);
    }

    public void AddUser(TwitterUser user)
    {
      if (!_users.ContainsKey(user.Id))
      {
        _users.Add(user.Id, user);
      }

      UserAdded?.Invoke(this, new UserAddedEventArgs(user.Id, user.Handle));
    }

    public bool UserTracked(long id)
    {
      return _users.ContainsKey(id);
    }

    public QueueItem GetOrAdd(QueueItem item)
    {
      return _items.GetOrAdd(item.TweetId, item);
    }

    public int IncreasePlayCount(QueueItem item)
    {
      return _items.AddOrUpdate(item.TweetId, item, Update).Played;

      QueueItem Update(long key, QueueItem existing)
      {
        existing.Played++;
        return existing;
      }
    }

    public IEnumerable<QueueItem> GetNextQueueItems(int count)
    {
      for (int i = 0; i < count; i++)
      {
        if (_queue.TryDequeue(out long id) &&
          _items.TryGetValue(id, out QueueItem item))
        {
          yield return item;
        }
      }
    }
  }
}
