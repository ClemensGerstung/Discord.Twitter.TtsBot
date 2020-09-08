﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;
using Discord.WebSocket;
using Google.Cloud.TextToSpeech.V1;
using Tweetinvi;
using Tweetinvi.Streaming;
using Stream = System.IO.Stream;
using TwitterStream = Tweetinvi.Stream;
using Tweetinvi.Models;
using System.Linq;
using Tweetinvi.Events;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using log4net;
using CliWrap;
using System.IO;
using Discord.Twitter.TtsBot.AdminAccess;
using SNWS = System.Net.WebSockets;
using System.Collections.Generic;
using Google.Protobuf;
using Tweetinvi.Logic.TwitterEntities;

namespace Discord.Twitter.TtsBot
{
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

  public class TtsBot
  {
    private ILog __log = LogManager.GetLogger(typeof(TtsBot));

    private Option _option;
    private DiscordSocketClient _discord;
    private TextToSpeechClient _ttsClient;
    private ITwitterCredentials _userCredentials;
    private IGuildUser _currentUser;
    private Regex _tweetIdRegex;
    private AdminAccess.AdminAccess.AdminAccessClient _grpcClient;
    private DataStore _store;
    private IDictionary<SNWS.WebSocket, TaskCompletionSource<object>> _webSockets;

    private IFilteredStream _stream;
    private Task _twitterStreamTask;

    public TtsBot(Option option, AdminAccess.AdminAccess.AdminAccessClient grpcClient, DataStore store)
    {
      _option = option ?? throw new ArgumentNullException(nameof(option));
      _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
      _store = store ?? throw new ArgumentNullException(nameof(store));
      _tweetIdRegex = new Regex(@"\/(?<tweetId>\d+)");
      _webSockets = new Dictionary<SNWS.WebSocket, TaskCompletionSource<object>>();

      if (option.GoogleUseEnvironmentVariable)
      {
        _ttsClient = TextToSpeechClient.Create();
      }
      else
      {
        if (string.IsNullOrWhiteSpace(_option.GoogleApplicationCredentialsPath))
          throw new ArgumentNullException(nameof(_option.GoogleApplicationCredentialsPath), "Credential path cannot be empty if not using Enviornment Variable \"GOOGLE_APPLICATION_CREDENTIALS\"");

        TextToSpeechClientBuilder builder = new TextToSpeechClientBuilder();
        builder.CredentialsPath = _option.GoogleApplicationCredentialsPath;

        _ttsClient = builder.Build();
      }

      _discord = new DiscordSocketClient();
      _discord.Log += obj =>
      {
        Console.WriteLine(obj);
        return Task.CompletedTask;
      };

      _userCredentials = Auth.CreateCredentials(option.TwitterConsumerKey,
                                                option.TwitterConsumerSecret,
                                                option.TwitterUserAccessToken,
                                                option.TwitterUserAccessSecret);
      Auth.SetCredentials(_userCredentials);

      _stream = TwitterStream.CreateFilteredStream(_userCredentials, TweetMode.Extended);
      _stream.MatchingTweetReceived += OnMatchingTweetReceived;

      _store.UserAdded += OnUserAddedToDataStore;
    }

    private void OnUserAddedToDataStore(object sender, UserAddedEventArgs args)
    {
      _stream.PauseStream();
      _stream.AddFollow(args.UserId);
      _stream.ResumeStream();
      
      _ = BroadcastAsync(_store.GetUser(args.UserId));
    }

    public async Task StartAsync()
    {
      using ManualResetEventSlim streamStartedEvent = new ManualResetEventSlim(false);
      _discord.MessageReceived += OnDiscordMessageReceived;
      await _discord.LoginAsync(TokenType.Bot, _option.DiscordToken);
      await _discord.StartAsync();

      _stream.StreamStarted += OnStreamStarted;
      _twitterStreamTask = Task.Factory.StartNew(_stream.StartStreamMatchingAnyCondition,
                                                 CancellationToken.None,
                                                 TaskCreationOptions.LongRunning,
                                                 TaskScheduler.Default);
      streamStartedEvent.Wait();

      void OnStreamStarted(object sender, EventArgs args) => streamStartedEvent.Set();
    }

    public async Task ShutdownAsync()
    {
      _stream.StopStream();
      await _twitterStreamTask;

      await _discord.StopAsync();
      await _discord.LogoutAsync();
    }

    internal Task<IAudioClient> BeginSoundAsync()
    {
      if (_currentUser == null) return null;

      return _currentUser.VoiceChannel.ConnectAsync();
    }

    internal Task EndSoundAsync(IAudioClient audioClient)
    {
      return audioClient.StopAsync();
      //return _currentUser.VoiceChannel.DisconnectAsync();
    }

    internal void AddWebSocket(SNWS.WebSocket webSocket, TaskCompletionSource<object> tcs)
    {
      _webSockets.Add(webSocket, tcs);
    }

    private async Task BroadcastAsync(Google.Protobuf.IMessage message)
    {
      //QueueItem item = new QueueItem();
      using MemoryStream stream = new MemoryStream();

      message.WriteTo(stream);
      stream.Seek(0, SeekOrigin.Begin);

      ReadOnlyMemory<byte> readOnlyMemory = new ReadOnlyMemory<byte>();
      await stream.WriteAsync(readOnlyMemory);

      bool res = stream.TryGetBuffer(out ArraySegment<byte> array);

      foreach (var kvp in _webSockets.ToArray())
      {
        var webSocket = kvp.Key;
        if (webSocket.CloseStatus.HasValue)
        {
          await webSocket.CloseAsync(SNWS.WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
          _webSockets.Remove(webSocket);
          kvp.Value.SetResult(new object());
          continue;
        }

        await webSocket.SendAsync(array, SNWS.WebSocketMessageType.Binary, true, CancellationToken.None);
      }
    }

    public void PrintMessage(Google.Protobuf.IMessage message)
    {

      var descriptor = message.Descriptor;
      if (descriptor.ClrType == typeof(QueueItem))
      {
        var item = (QueueItem)message;

      }
    }

    private async Task OnDiscordMessageReceived(SocketMessage message)
    {
      if (!message.Content.StartsWith(_option.CommandPrefix, StringComparison.InvariantCultureIgnoreCase))
      {
        return;
      }

      string[] arguments = message.Content
                                  .Split(' ',
                                         StringSplitOptions.RemoveEmptyEntries)
                                  .Skip(1)
                                  .ToArray();

      if (arguments.Length == 1 &&
        arguments[0] == "join")
      {
        var user = message.Author as IGuildUser;
        if (_currentUser == null)
        {
          _currentUser = user;
          await message.Channel.SendMessageAsync($"Hello {user.Mention}, I'll update you on my awesome tweets!");
        }
        else if (Equals(_currentUser, user))
        {
          await message.Channel.SendMessageAsync($"I'm already updating you on my tweets, {user.Mention}!");
        }
        else
        {
          await message.Channel.SendMessageAsync($"I'm already updating {_currentUser.Mention} on my tweets, {user.Mention}!");
        }
      }

      //if (arguments.Length == 2 &&
      //  arguments[0] == "update" &&
      //  int.TryParse(arguments[1], out int count))
      //{
      //  // todo: check if count > 200
      //  IEnumerable<ITweet> tweets = await _twitterUser.GetUserTimelineAsync(count);
      //  foreach (ITweet tweet in tweets)
      //  {
      //    _queue.Enqueue(tweet);
      //  }
      //  _playSoundSignal.Set();
      //}

      if (arguments.Length == 2 &&
        arguments[0] == "read")
      {
        string link = arguments[1];
        Match match = _tweetIdRegex.Match(link);
        if (long.TryParse(match.Groups["tweetId"].Value, out long id))
        {
          var request = new AddQueueRequest
          {
            TweetId = id
          };
          var response = _grpcClient.AddQueueItem(request);
          var readResponse = _grpcClient.ReadNextQueueItems(new ReadNextQueueItemsRequest());
        }
      }
    }

    private void OnMatchingTweetReceived(object sender, MatchedTweetReceivedEventArgs args)
    {
      if (_store.UserTracked(args.Tweet.CreatedBy.Id))
      {
        var request = new AddQueueRequest
        {
          TweetId = args.Tweet.Id
        };
        var response = _grpcClient.AddQueueItem(request);

        //_ = BroadcastAsync(1);

        var readResponse = _grpcClient.ReadNextQueueItems(new ReadNextQueueItemsRequest());
      }
    }

    private async Task GetTweetAudioAsync(string ssml, string voiceName, string languageCode, Stream destination)
    {
      string args = "-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1";
      var request = new SynthesizeSpeechRequest
      {
        Input = new SynthesisInput
        {
          Ssml = ssml
        },
        Voice = new VoiceSelectionParams
        {
          LanguageCode = languageCode,
          Name = voiceName
        },
        AudioConfig = new AudioConfig
        {
          AudioEncoding = AudioEncoding.OggOpus
        }
      };

      var response = await _ttsClient.SynthesizeSpeechAsync(request);

      using MemoryStream audioContent = new MemoryStream();
      response.AudioContent.WriteTo(audioContent);

      audioContent.Seek(0, SeekOrigin.Begin);
      await (audioContent | Cli.Wrap("ffmpeg.exe").WithArguments(args) | destination).ExecuteAsync();
    }

    internal async Task PlayTweetAsync(IAudioClient client, string text, string voiceName, string languageCode)
    {
      using Stream discord = client.CreatePCMStream(AudioApplication.Mixed);
      using MemoryStream buffer = new MemoryStream();

      try
      {
        await GetTweetAudioAsync(text,
                                 voiceName,
                                 languageCode,
                                 buffer);

        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(discord);
      }
      catch (Exception exception)
      {
        __log.Error(exception);
      }
      finally
      {
        await discord.FlushAsync();
      }
    }
  }
}
