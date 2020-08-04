using Discord.Twitter.TtsBot.AdminAccess;
using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tweetinvi;

namespace Discord.Twitter.TtsBot
{
  using IUser = Tweetinvi.Models.IUser;

  class Impl : AdminAccess.AdminAccess.AdminAccessBase
  {
    private IDictionary<long, TwitterUser> _users = new ConcurrentDictionary<long, TwitterUser>();
    private IDictionary<long, QueueItem> _items = new ConcurrentDictionary<long, QueueItem>();
    private ConcurrentQueue<QueueItem> _queue = new ConcurrentQueue<QueueItem>();

    public ICollection<TwitterUser> Users => _users.Values;

    public override async Task<AddQueueResponse> AddQueueItem(AddQueueRequest request, ServerCallContext context)
    {
      AddQueueResponse response = new AddQueueResponse();

      switch (request.NewItemCase)
      {
        case AddQueueRequest.NewItemOneofCase.Item:
          response.Item = request.Item;
          break;
        case AddQueueRequest.NewItemOneofCase.TweetId:
          var tweet = await TweetAsync.GetTweet(request.TweetId);

          // TODO: read tweet's content

          break;
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

      if(!_users.ContainsKey(user.Id))
      {
        _users.Add(user.Id, user);
      }

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
      var items = _queue.ToList();
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

    public override Task<ReadItemsResponse> ReadItems(ReadItemsRequest request, ServerCallContext context)
    {
      return base.ReadItems(request, context);
    }

    public override Task<ReadItemsResponse> ReadNextQueueItems(ReadNextQueueItemsRequest request, ServerCallContext context)
    {
      return base.ReadNextQueueItems(request, context);
    }
  }
}
