using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Discord.Twitter.TtsBot.AdminAccess;
using Tweetinvi.Core.Extensions;

namespace Discord.Twitter.TtsBot
{
  public class DataStore
  {
    private readonly ConcurrentDictionary<long, TwitterUser> _users = new ConcurrentDictionary<long, TwitterUser>();
    private readonly ConcurrentDictionary<long, QueueItem> _items = new ConcurrentDictionary<long, QueueItem>();
    private readonly ConcurrentQueue<long> _queue = new ConcurrentQueue<long>();
    private readonly DatabaseContext _context;

    public ICollection<TwitterUser> Users => _users.Values;

    public IList<QueueItem> QueueItems => _queue.Select(id => _items[id])
                                                .ToList();

    public ICollection<QueueItem> Items => _items.Values;

    public event EventHandler<UserChangedEventArgs> UsersChanged;
    public event EventHandler<ItemsChangedEventArgs> ItemsChanged;
    public event EventHandler<ItemsChangedEventArgs> QueueChanged;
    public event EventHandler<ItemPlayedEventArgs> ItemPlayed;

    public DataStore(DatabaseContext context)
    {
      _context = context;
      _context.Database.EnsureCreated();

      _context.Users
              .ForEach(user => _users.TryAdd(user.Id, user));

      _context.Items.ForEach(item => _items.TryAdd(item.TweetId, item));
    }

    public void Enqueue(QueueItem item)
    {
      QueueItem queueItem = GetOrAdd(item);
      _queue.Enqueue(queueItem.TweetId);
      QueueChanged?.Invoke(this, new ItemsChangedEventArgs(queueItem, null));
    }

    public void AddUser(TwitterUser user)
    {
      if (_users.TryAdd(user.Id, user))
      {
        _context.Add(user);
        _context.SaveChanges();
        UsersChanged?.Invoke(this, new UserChangedEventArgs(user, null));
      }
    }

    public bool RemoveUser(TwitterUser user)
    {
      if (_users.TryRemove(user.Id, out _))
      {
        user = _context.Find<TwitterUser>(user.Id);

        _context.Remove(user);
        _context.SaveChanges();
        UsersChanged?.Invoke(this, new UserChangedEventArgs(null, user));

        return true;
      }

      return false;
    }

    public bool UserTracked(long id)
    {
      return _users.ContainsKey(id);
    }

    public TwitterUser GetUser(long id)
    {
      _users.TryGetValue(id, out TwitterUser user);

      return user;
    }

    public QueueItem GetOrAdd(QueueItem item)
    {
      if (_items.TryAdd(item.TweetId, item))
      {
        _context.Add(item);
        _context.SaveChanges();

        ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(item, null));
      }

      return item;
    }

    public int IncreasePlayCount(QueueItem item)
    {
      QueueItem changed = _items.AddOrUpdate(item.TweetId, item, Update);
      ItemPlayed?.Invoke(this, new ItemPlayedEventArgs(changed));

      return changed.Played;

      QueueItem Update(long key, QueueItem existing)
      {
        existing.Played++;
        return existing;
      }
    }

    public bool Dequeue(out QueueItem item)
    {
      if(_queue.TryDequeue(out long id))
      {
        item = _items[id];

        QueueChanged?.Invoke(this, new ItemsChangedEventArgs(null, item));
        return true;
      }

      item = null;
      return false;
    }

    public IEnumerator<QueueItem> GetQueueEnumerator(int count)
    {
      return new QueueEnumerator(count, this);
    }

    private class QueueEnumerator : IEnumerator<QueueItem>
    {
      private DataStore _store;
      private int _count;
      private int _index;
      private QueueItem _item;

      public QueueItem Current => _item;

      object IEnumerator.Current => this.Current;

      public QueueEnumerator(int count, DataStore store)
      {
        _index = 0;
        _count = count;
        _store = store;
      }

      public void Dispose()
      {
      }

      public bool MoveNext()
      {
        return (_count == _index++) && 
               _store.Dequeue(out _item);
      }

      public void Reset()
      {
        throw new NotSupportedException();
      }
    }
  }
}
