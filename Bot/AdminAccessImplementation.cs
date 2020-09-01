using Discord.Twitter.TtsBot.AdminAccess;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;

namespace Discord.Twitter.TtsBot
{
  using IUser = Tweetinvi.Models.IUser;

  public class ReadItemEventArgs : EventArgs
  {
    public string Content { get; }

    internal ReadItemEventArgs(string content)
    {
      this.Content = content;
    }
  }

  public class UserAddedEventArgs : EventArgs
  {
    public string ScreenName { get; }

    public long UserId { get; }

    public UserAddedEventArgs(long userId, string screenName)
    {
      ScreenName = screenName;
      UserId = userId;
    }
  }

  public class Impl : AdminAccess.AdminAccess.AdminAccessBase, 
                      IDisposable
  {
    private readonly IDictionary<long, TwitterUser> _users = new ConcurrentDictionary<long, TwitterUser>();
    private readonly ConcurrentDictionary<long, QueueItem> _items = new ConcurrentDictionary<long, QueueItem>();
    private readonly ConcurrentQueue<long> _queue = new ConcurrentQueue<long>();
    private readonly Regex _urlRegex = new Regex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");

    private readonly ILogger<Impl> _logger;
    private readonly TtsBot _bot;

    public ICollection<TwitterUser> Users => _users.Values;

    public event EventHandler<UserAddedEventArgs> UserAdded;

    public Impl(ILogger<Impl> logger, TtsBot bot)
    {
      _logger = logger;
      _bot = bot;
    }


    ~Impl()
    {
      this.Dispose(false);
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      
    }

    public override async Task<AddQueueResponse> AddQueueItem(AddQueueRequest request, ServerCallContext context)
    {
      AddQueueResponse response = new AddQueueResponse();
      QueueItem item = null;

      switch (request.NewItemCase)
      {
        case AddQueueRequest.NewItemOneofCase.Item:
          response.Item = request.Item;
          break;
        case AddQueueRequest.NewItemOneofCase.TweetId:
          ITweet tweet = await TweetAsync.GetTweet(request.TweetId);
          item = new QueueItem();
          item.TweetId = tweet.Id;
          item.Played = 0;

          string text = tweet.FullText;
          if (tweet.IsRetweet)
          {
            ITweet retweeted = tweet.RetweetedTweet;
            text = retweeted.FullText;
          }

          if (tweet.QuotedTweet != null)
          {
            ITweet quoted = tweet.QuotedTweet;
            text = quoted.FullText;
          }

          text = _urlRegex.Replace(text, "");
          if (string.IsNullOrWhiteSpace(text))
          {
            response.IsEmpty = true;
            break;
          }

          item.Content = text;

          response.Item = item;
          break;
      }

      if (!response.IsEmpty)
      {
        _queue.Enqueue(item.TweetId);
        _items.AddOrUpdate(item.TweetId, item, (id, old) => item);
      }

      return response;
    }

    public override async Task<GetTwitterUserResponse> AddTwitterUser(AddTwitterUserRequest request, ServerCallContext context)
    {
      GetTwitterUserRequest twitterUserRequest = new GetTwitterUserRequest();
      twitterUserRequest.Handle = request.Handle;
      twitterUserRequest.Id = request.Id;

      GetTwitterUserResponse response = await GetTwitterUser(twitterUserRequest, null);
      var user = response.User;

      if (!_users.ContainsKey(user.Id))
      {
        _users.Add(user.Id, user);
      }

      UserAdded?.Invoke(this, new UserAddedEventArgs(user.Id, user.Handle));

      return response;
    }

    public override Task<GetAllTwitterUserRespone> GetAllTwitterUsers(GetAllTwitterUserRequest request, ServerCallContext context)
    {
      GetAllTwitterUserRespone response = new GetAllTwitterUserRespone();
      response.Users.Add(Users);

      return Task.FromResult(response);
    }

    public override Task<GetQueueResponse> GetQueue(GetQueueRequest request, ServerCallContext context)
    {
      GetQueueResponse response = new GetQueueResponse();
      var items = _queue.Select(id => _items[id])
                        .ToList();
      var from = request.From ?? items.Min(i => i.Created);
      var to = request.To ?? items.Max(i => i.Created);

      response.Items
              .Add(items.Where(q => request.Users.Contains(q.User))
                        .Where(q => q.Content.Contains(request.Filter))
                        .Where(q => q.Created >= from && q.Created < to));

      return Task.FromResult(response);
    }

    public override Task<GetQueueResponse> GetReadItems(GetQueueRequest request, ServerCallContext context)
    {
      GetQueueResponse response = new GetQueueResponse();

      var items = _items.Values.ToList();
      var from = request.From ?? items.Min(i => i.Created);
      var to = request.To ?? items.Max(i => i.Created);

      response.Items
              .Add(items.Where(q => q.Played > 0)
                        .Where(q => request.Users.Contains(q.User))
                        .Where(q => q.Content.Contains(request.Filter))
                        .Where(q => q.Created >= from && q.Created < to));

      return Task.FromResult(response);
    }

    public override async Task<GetTwitterUserResponse> GetTwitterUser(GetTwitterUserRequest request, ServerCallContext context)
    {
      GetTwitterUserResponse respone = new GetTwitterUserResponse();

      IUser user = request.UserCase switch
      {
        GetTwitterUserRequest.UserOneofCase.Id => await UserAsync.GetUserFromId(request.Id),
        GetTwitterUserRequest.UserOneofCase.Handle => await UserAsync.GetUserFromScreenName(request.Handle),
        _ => null,
      };

      respone.User.Id = user.Id;
      respone.User.Handle = user.ScreenName;
      respone.User.Name = user.Name;

      return respone;
    }

    public override async Task<ReadItemsResponse> ReadItems(ReadItemsRequest request, ServerCallContext context)
    {
      ReadItemsResponse response = new ReadItemsResponse();

      var audioClient = await _bot.BeginSoundAsync();
      foreach (var item in request.QueueItems)
      {
        var old = _items.GetOrAdd(item.TweetId, item);
        await _bot.PlayTweetAsync(audioClient, item.Content, "en-US-Wavenet-C", "en-US");

        item.Played++;
        _items.TryUpdate(item.TweetId, item, old);

        response.QueueItems.Add(item);
      }
      await _bot.EndSoundAsync();

      return response;
    }

    public override Task<ReadItemsResponse> ReadNextQueueItems(ReadNextQueueItemsRequest request, ServerCallContext context)
    {
      ReadItemsRequest readRequest = new ReadItemsRequest();

      for (int i = 0; i < request.Count; i++)
      {
        if (_queue.TryDequeue(out long id) &&
          _items.TryGetValue(id, out QueueItem item))
        {
          readRequest.QueueItems.Add(item);
        }
      }

      return ReadItems(readRequest, context);
    }
  }
}
