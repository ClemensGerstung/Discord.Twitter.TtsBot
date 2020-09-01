using System;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;
using Discord.WebSocket;
using Google.Cloud.TextToSpeech.V1;
using Tweetinvi;
using Tweetinvi.Streaming;
using TwitterUser = Tweetinvi.Models.IUser;
using Stream = System.IO.Stream;
using TwitterStream = Tweetinvi.Stream;
using Tweetinvi.Models;
using System.Linq;
using Tweetinvi.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using log4net;
using CliWrap;
using System.IO;

namespace Discord.Twitter.TtsBot
{
  public class TtsBot
  {
    private ILog __log = LogManager.GetLogger(typeof(TtsBot));

    private ManualResetEventSlim _finalizeSignal;
    private Option _option;
    private DiscordSocketClient _discord;
    private TextToSpeechClient _ttsClient;
    private ITwitterCredentials _userCredentials;
    private IGuildUser _currentUser;
    private ConcurrentQueue<ITweet> _queue;
    private ManualResetEventSlim _playSoundSignal;
    private Regex _regex;
    private Regex _tweetIdRegex;
    private AdminAccess.AdminAccess.AdminAccessClient _grpcClient;

    private IFilteredStream _stream;
    private Task<Task> _twitterStreamTask;
    private Task _playSoundTask;

    public Exception Exception { get; private set; }

    public TtsBot(Option option, AdminAccess.AdminAccess.AdminAccessClient grpcClient)
    {
      _option = option ?? throw new ArgumentNullException(nameof(option));
      _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
      _finalizeSignal = new ManualResetEventSlim(false);
      _playSoundSignal = new ManualResetEventSlim(false);
      _queue = new ConcurrentQueue<ITweet>();
      _regex = new Regex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");
      _tweetIdRegex = new Regex(@"\/(?<tweetId>\d+)");

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

      _userCredentials = Auth.CreateCredentials(option.TwitterConsumerKey,
                                                option.TwitterConsumerSecret,
                                                option.TwitterUserAccessToken,
                                                option.TwitterUserAccessSecret);
      Auth.SetCredentials(_userCredentials);

      _stream = TwitterStream.CreateFilteredStream(_userCredentials, TweetMode.Extended);
      _stream.MatchingTweetReceived += OnMatchingTweetReceived;
    }

    private async Task PlayTweetAsync()
    {
      // kick off to different thread
      await Task.Delay(1);

      do
      {
        _playSoundSignal.Wait();
        if (_finalizeSignal.IsSet) break;

        if (_currentUser?.VoiceChannel == null)
        {
          _playSoundSignal.Reset();
          continue;
        }

        IAudioClient client = await _currentUser.VoiceChannel.ConnectAsync();

        do
        {
          if (_queue.TryDequeue(out ITweet tweet))
          {
            //await PlayTweetAsync(client, tweet);
          }

          if (_finalizeSignal.IsSet) break;
        } while (!_queue.IsEmpty);


        _playSoundSignal.Reset();

      } while (!_finalizeSignal.IsSet);
    }

    public async Task StartAsync()
    {
      using ManualResetEventSlim streamStartedEvent = new ManualResetEventSlim(false);
      _discord.MessageReceived += OnDiscordMessageReceived;
      await _discord.LoginAsync(TokenType.Bot, _option.DiscordToken);
      await _discord.StartAsync();

      _stream.StreamStarted += OnStreamStarted;
      _twitterStreamTask = Task.Factory.StartNew(_stream.StartStreamMatchingAnyConditionAsync,
                                              CancellationToken.None,
                                              TaskCreationOptions.LongRunning,
                                              TaskScheduler.Default);
      streamStartedEvent.Wait();

      _playSoundTask = PlayTweetAsync();

      void OnStreamStarted(object sender, EventArgs args) => streamStartedEvent.Set();
    }

    public async Task ShutdownAsync()
    {
      _finalizeSignal.Set();
      _playSoundSignal.Set();

      _stream.StopStream();
      await _twitterStreamTask;

      await _playSoundTask;

      await _discord.StopAsync();
      await _discord.LogoutAsync();
    }

    internal async Task<IAudioClient> BeginSoundAsync()
    {
      if (_currentUser == null) return null;

      return await _currentUser.VoiceChannel.ConnectAsync();
    }

    internal async Task EndSoundAsync()
    {
      await _currentUser.VoiceChannel.DisconnectAsync();
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

      //if (arguments.Length == 2 &&
      //  arguments[0] == "read")
      //{
      //  string link = arguments[1];
      //  Match match = _tweetIdRegex.Match(link);
      //  if (long.TryParse(match.Groups["tweetId"].Value, out long id))
      //  {
      //    ITweet tweet = await TweetAsync.GetTweet(id);
      //    if (tweet.CreatedBy.Id == _twitterUser.Id)
      //    {
      //      _queue.Enqueue(tweet);
      //      _playSoundSignal.Set();
      //    }
      //  }
      //}
    }

    private void OnMatchingTweetReceived(object sender, MatchedTweetReceivedEventArgs args)
    {
      //if (args.Tweet.CreatedBy.Id != _twitterUser.Id) return;

      _queue.Enqueue(args.Tweet);
      _playSoundSignal.Set();
    }

    private async Task GetTweetAudioAsync(string text, string voiceName, string languageCode, Stream destination)
    {
      string args = "-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1";
      //string args = "-hide_banner -loglevel panic -i pipe:0 -ac 2 -f mp3 -ar 48000 pipe:1";
      var request = new SynthesizeSpeechRequest
      {
        Input = new SynthesisInput
        {
          Text = text
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
