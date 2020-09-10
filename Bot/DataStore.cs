﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.Twitter.TtsBot.AdminAccess;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Tweetinvi.Core.Extensions;

namespace Discord.Twitter.TtsBot
{
  public class DatabaseContext : DbContext
  {
    public DbSet<TwitterUser> Users { get; set; }
    public DbSet<QueueItem> Items { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      base.OnConfiguring(optionsBuilder);

      optionsBuilder.UseSqlite("Data Source=datastorage.db",
        options =>
        {
          //options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
        });
      optionsBuilder.EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      modelBuilder.Entity<TwitterUser>()
                  .ToTable("Users")
                  .HasKey(user => user.Id);
      modelBuilder.Entity<QueueItem>()
                  .ToTable("Items")
                  .HasKey(qi => qi.TweetId);

      modelBuilder.Entity<QueueItem>()
                  .Property(qi => qi.Created)
                  .HasConversion(ts => ts.ToDateTime().ToString("s"),
                                 v => Timestamp.FromDateTime(DateTime.Parse(v)));
    }

  }

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

    public DataStore(DatabaseContext context)
    {
      _context = context;
      _context.Database.EnsureCreated();

      _context.Users
              .ForEach(user => _users.TryAdd(user.Id, user));
    }

    public void Enqueue(QueueItem item)
    {
      _queue.Enqueue(GetOrAdd(item).TweetId);
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
      }

      return item;
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
      if (count == 0) count = _queue.Count;

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
