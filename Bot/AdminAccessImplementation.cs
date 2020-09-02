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

  public class Impl : AdminAccess.AdminAccess.AdminAccessBase, 
                      IDisposable
  {
    private readonly Regex _urlRegex = new Regex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");

    private readonly ILogger<Impl> _logger;
    private readonly TtsBot _bot;
    private readonly DataStore _dataStore;

    public Impl(ILogger<Impl> logger, TtsBot bot, DataStore dataStore)
    {
      _logger = logger;
      _bot = bot;
      _dataStore = dataStore;
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
          item.User = (await GetTwitterUser(new GetTwitterUserRequest { Id = tweet.CreatedBy.Id }, context)).User;

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
        _dataStore.Enqueue(item);
      }

      return response;
    }

    public override async Task<GetTwitterUserResponse> AddTwitterUser(GetTwitterUserRequest request, ServerCallContext context)
    {
      GetTwitterUserResponse response = await GetTwitterUser(request, context);
      _dataStore.AddUser(response.User);

      return response;
    }

    public override Task<GetAllTwitterUserRespone> GetAllTwitterUsers(GetAllTwitterUserRequest request, ServerCallContext context)
    {
      GetAllTwitterUserRespone response = new GetAllTwitterUserRespone();
      response.Users.Add(_dataStore.Users);

      return Task.FromResult(response);
    }

    public override Task<GetQueueResponse> GetQueue(GetQueueRequest request, ServerCallContext context)
    {
      GetQueueResponse response = new GetQueueResponse();
      var items = _dataStore.QueueItems;
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

      var items = _dataStore.Items;
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
      GetTwitterUserResponse response = new GetTwitterUserResponse();
      response.User = new TwitterUser { Language = request.Language, VoiceName = request.VoiceName };

      IUser user = request.UserCase switch
      {
        GetTwitterUserRequest.UserOneofCase.Id => await UserAsync.GetUserFromId(request.Id),
        GetTwitterUserRequest.UserOneofCase.Handle => await UserAsync.GetUserFromScreenName(request.Handle),
        _ => null,
      };

      response.User.Id = user.Id;
      response.User.Handle = user.ScreenName;
      response.User.Name = user.Name;

      return response;
    }

    public override async Task<ReadItemsResponse> ReadItems(ReadItemsRequest request, ServerCallContext context)
    {
      ReadItemsResponse response = new ReadItemsResponse();

      var audioClient = await _bot.BeginSoundAsync();
      foreach (var item in request.QueueItems)
      {
        _dataStore.GetOrAdd(item);

        await _bot.PlayTweetAsync(audioClient, item.Content, item.User.VoiceName, item.User.Language);

        item.Played++;
        _dataStore.IncreasePlayCount(item);

        response.QueueItems.Add(item);
      }
      await _bot.EndSoundAsync();

      return response;
    }

    public override Task<ReadItemsResponse> ReadNextQueueItems(ReadNextQueueItemsRequest request, ServerCallContext context)
    {
      ReadItemsRequest readRequest = new ReadItemsRequest();

      readRequest.QueueItems.AddRange(_dataStore.GetNextQueueItems(request.Count));

      return ReadItems(readRequest, context);
    }
  }
}
