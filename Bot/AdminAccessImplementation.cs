using Discord.Twitter.TtsBot.AdminAccess;
using Google.Protobuf.WellKnownTypes;
using Google.Type;
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
using System.Web;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Models.Entities;

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
          item = response.Item = request.Item;
          break;
        case AddQueueRequest.NewItemOneofCase.TweetId:
          ITweet tweet = await TweetAsync.GetTweet(request.TweetId);
          item = new QueueItem();
          item.TweetId = tweet.Id;
          item.Played = 0;
          item.User = _dataStore.GetUser(tweet.CreatedBy.Id);
          item.Created = tweet.CreatedAt
                              .ToUniversalTime()
                              .ToTimestamp();

          StringBuilder builder = new StringBuilder("<speak>");
          ISet <IUserMentionEntity> users = new HashSet<IUserMentionEntity>(tweet.UserMentions);
          
          if (tweet.IsRetweet)
          {
            ITweet retweeted = tweet.RetweetedTweet;
            builder.AppendFormat("I, {0} retweetet {1}:",
                                 tweet.CreatedBy.Name,
                                 tweet.CreatedBy.Id == retweeted.CreatedBy.Id ?
                                   "myself" :
                                   retweeted.CreatedBy.Name);
            builder.Append("<break time=\"500ms\"/>");
            builder.Append(retweeted.FullText);
            
            foreach (var userMention in retweeted.UserMentions)
            {
              users.Add(userMention);
            }
          }
          else if (tweet.QuotedTweet != null)
          {
            ITweet quoted = tweet.QuotedTweet;
            builder.AppendFormat("I, {0} quoted {1}: ",
                                 tweet.CreatedBy.Name,
                                 tweet.CreatedBy.Id == quoted.CreatedBy.Id ?
                                   "myself" :
                                   quoted.CreatedBy.Name);
            builder.Append("<break time=\"500ms\"/>");
            builder.Append(quoted.FullText);
            builder.Append("<break time=\"500ms\"/>");
            builder.AppendFormat("My quote on that: {0}", tweet.FullText);

            foreach (var userMention in quoted.UserMentions)
            {
              users.Add(userMention);
            }
          }
          else
          {
            builder.AppendFormat("I, {0} tweeted: ",
                                 tweet.CreatedBy.Name);
            builder.Append(tweet.FullText);
          }

          builder.Append("<break time=\"1s\"/>");
          builder.Append("</speak>");

          string text = _urlRegex.Replace(builder.ToString(), "");
          if (string.IsNullOrWhiteSpace(text))
          {
            response.IsEmpty = true;
            break;
          }

          foreach (var user in users)
          {
            text = text.Replace($"@{user.ScreenName}", user.Name);
          }
          
          item.Ssml = HttpUtility.HtmlDecode(text);
          item.Content = HttpUtility.HtmlDecode(tweet.FullText);
          response.Item = item;
          break;
      }

      if (!response.IsEmpty)
      {
        _logger.LogInformation("Added Item {0} by {1}", item.TweetId, item.User.Handle);
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

    public override Task<GetTwitterUserResponse> RemoveTwitterUser(RemoveTwitterUserRequest request, ServerCallContext context)
    {
      var user = request.TwitterUser;
      if(request.UserCase == RemoveTwitterUserRequest.UserOneofCase.Id)
      {
        user = _dataStore.GetUser(request.Id);
      }

      if(_dataStore.RemoveUser(user))
      {
        return Task.FromResult(new GetTwitterUserResponse { User = user });
      }

      return Task.FromResult(new GetTwitterUserResponse());
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
      
      response.Items
              .Add(FilterQueueItems(items, request));

      return Task.FromResult(response);
    }

    public override Task<GetQueueResponse> GetItems(GetQueueRequest request, ServerCallContext context)
    {
      GetQueueResponse response = new GetQueueResponse();
      var items = _dataStore.Items;
      
      response.Items
              .Add(FilterQueueItems(items, request));

      return Task.FromResult(response);
    }

    public override Task<GetQueueResponse> GetReadItems(GetQueueRequest request, ServerCallContext context)
    {
      GetQueueResponse response = new GetQueueResponse();

      var items = _dataStore.Items;
      
      response.Items
              .Add(FilterQueueItems(items.Where(q => q.Played > 0), request));

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
      response.User.ProfileImageUrl = user.ProfileImageUrlFullSize;

      return response;
    }

    public override async Task<ReadItemsResponse> ReadItems(ReadItemsRequest request, ServerCallContext context)
    {
      ReadItemsResponse response = new ReadItemsResponse();
      var enumerator = request.QueueItems.GetEnumerator();

      var items = await PlayQueueItemsAsync(AquireItem);
      response.QueueItems.Add(items);

      return response;

      Task<QueueItem> AquireItem()
      {
        if (enumerator.MoveNext())
        {
          return Task.FromResult(enumerator.Current);
        }

        return Task.FromResult((QueueItem)null);
      }
    }

    public override async Task<ReadItemsResponse> ReadNextQueueItems(ReadNextQueueItemsRequest request, ServerCallContext context)
    {
      ReadItemsResponse response = new ReadItemsResponse();

      var enumerator = _dataStore.GetQueueEnumerator(request.Count);

      var items = await PlayQueueItemsAsync(AquireItem);
      response.QueueItems.Add(items);

      return response;

      Task<QueueItem> AquireItem()
      {
        if (enumerator.MoveNext())
        {
          return Task.FromResult(enumerator.Current);
        }

        return Task.FromResult((QueueItem)null);
      }
    }

    private IEnumerable<QueueItem> FilterQueueItems(IEnumerable<QueueItem> items, GetQueueRequest request)
    {
      var from = request.From ?? items.Min(i => i.Created);
      var to = request.To ?? items.Max(i => i.Created);
      var users = request.Users.ToList();

      if (users.Count == 0)
      {
        users = _dataStore.Users.ToList();
      }

      return items.Where(q => users.Contains(q.User))
                  .Where(q => q.Content.Contains(request.Filter))
                  .Where(q => q.Created >= from && q.Created <= to);
    }

    private async Task<IEnumerable<QueueItem>> PlayQueueItemsAsync(Func<Task<QueueItem>> aquireItem)
    {
      var result = new List<QueueItem>();
      var audioClient = await _bot.BeginSoundAsync();
      if (audioClient == null)
      {
        return result;
      }

      QueueItem item;
      while((item = await aquireItem()) != null)
      {
        await _bot.PlayTweetAsync(audioClient, item.Ssml, item.User.VoiceName, item.User.Language);

        item.Played++;
        _dataStore.IncreasePlayCount(item);

        _logger.LogInformation("Played \"{0}\" for the {1} time", item.Content, item.Played);

        result.Add(item);
      }

      await _bot.EndSoundAsync(audioClient);

      return result;
    }
  }
}
