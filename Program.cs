using System;
using System.Diagnostics;
using System.IO;
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
using Newtonsoft.Json;
using Tweetinvi.Models;
using System.Linq;
using Tweetinvi.Events;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Discord.Twitter.TtsBot
{
  public class TtsBot
  {
    private ManualResetEventSlim _finalizeSignal;
    private Option _option;
    private DiscordSocketClient _discord;
    private TextToSpeechClient _ttsClient;
    private ITwitterCredentials _userCredentials;
    private IGuildUser _currentUser;
    private TwitterUser _twitterUser;
    private ConcurrentQueue<ITweet> _queue;
    private ManualResetEventSlim _playSoundSignal;
    private Regex _regex;

    public Exception Exception { get; private set; }

    public TtsBot(Option option)
    {
      _option = option ?? throw new ArgumentNullException(nameof(option));
      _finalizeSignal = new ManualResetEventSlim(false);
      _playSoundSignal = new ManualResetEventSlim(false);
      _queue = new ConcurrentQueue<ITweet>();
      _regex = new Regex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");

      if (option.GoogleSetEnvironmentVariable)
      {
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", option.GoogleApplicationCredentialsPath);
      }
      
      _discord = new DiscordSocketClient();
      _ttsClient = TextToSpeechClient.Create();

      _userCredentials = Auth.CreateCredentials(option.TwitterConsumerKey,
                                                option.TwitterConsumerSecret,
                                                option.TwitterUserAccessToken,
                                                option.TwitterUserAccessSecret);
      Auth.SetCredentials(_userCredentials);
    }

    private async Task PlayTweet()
    {
      do
      {
        _playSoundSignal.Wait();
        if (_currentUser?.VoiceChannel == null) 
        {
          _playSoundSignal.Reset();
          return; 
        }

        IAudioClient client = await _currentUser.VoiceChannel.ConnectAsync();

        do
        {
          if (_queue.TryDequeue(out ITweet tweet))
          {
            await PlayTweetAsync(client, tweet);
          }

          if (_finalizeSignal.IsSet) break;
        } while (!_queue.IsEmpty);

        await _currentUser.VoiceChannel.DisconnectAsync();
        _playSoundSignal.Reset();

      } while (!_finalizeSignal.IsSet);
    }

    public async Task RunAsync()
    {
      try
      {
        _discord.MessageReceived += OnDiscordMessageReceived;
        await _discord.LoginAsync(TokenType.Bot, _option.DiscordToken);
        await _discord.StartAsync();

        _twitterUser = User.GetUserFromScreenName(_option.FollowTwitterUser);
        
        IFilteredStream stream = TwitterStream.CreateFilteredStream(_userCredentials, TweetMode.Extended);
        stream.AddFollow(_twitterUser.Id);
        stream.MatchingTweetReceived += OnMatchingTweetReceived;
        Thread thread = new Thread(stream.StartStreamMatchingAllConditions);
        thread.Name = "TwitterStream";
        thread.Start();

        await PlayTweet();
        
        _finalizeSignal.Wait();
        stream.StopStream();
        thread.Join();

        await _discord.StopAsync();
        await _discord.LogoutAsync();
      }
      catch (Exception exception)
      {
        Exception = exception;
        _finalizeSignal.Set();
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

      if(arguments.Length == 1 &&
        arguments[0] == "join")
      {
        var user = message.Author as IGuildUser;
        if (_currentUser == null)
        {
          _currentUser = user;
          await message.Channel.SendMessageAsync($"Hello {user.Mention}, I'll update you on my awesome tweets!");
        }
        else if(Equals(_currentUser, user))
        {
          await message.Channel.SendMessageAsync($"I'm already updating you on my tweets, {user.Mention}!");
        }
        else
        {
          await message.Channel.SendMessageAsync($"I'm already updating {_currentUser.Mention} on my tweets, {user.Mention}!");
        }
      }

      if(arguments.Length == 2 &&
        arguments[0] == "update" &&
        int.TryParse(arguments[1], out int count))
      {
        // todo: check if count > 200
        IEnumerable<ITweet> tweets = await _twitterUser.GetUserTimelineAsync(count);
        foreach (ITweet tweet in tweets)
        {
          _queue.Enqueue(tweet);
        }
        _playSoundSignal.Set();
      }
    }

    private void OnMatchingTweetReceived(object sender, MatchedTweetReceivedEventArgs args)
    {
      if (args.Tweet.CreatedBy.Id != _twitterUser.Id) return;

      _queue.Enqueue(args.Tweet);
      _playSoundSignal.Set();
    }

    private async Task GetTweetAudioAsync(string text, Stream destination)
    {
      SynthesisInput input = new SynthesisInput
      {
        Text = text
      };

      VoiceSelectionParams voice = new VoiceSelectionParams
      {
        LanguageCode = "en-US",
        Name = _option.VoiceName
      };

      AudioConfig config = new AudioConfig
      {
        AudioEncoding = AudioEncoding.Linear16,
        SampleRateHertz = 48000 * 2
      };

      var response = await _ttsClient.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
      {
        Input = input,
        Voice = voice,
        AudioConfig = config
      });

      response.AudioContent.WriteTo(destination);
    }

    private async Task PlayTweetAsync(IAudioClient client, ITweet tweet)
    {
      using (Stream discord = client.CreatePCMStream(AudioApplication.Mixed))
      {
        try
        {
          string text = tweet.FullText;
          if(tweet.IsRetweet)
          {
            ITweet retweeted = tweet.RetweetedTweet;
            text = retweeted.FullText;
          }

          foreach (var user in tweet.UserMentions)
          {
            text = text.Replace("@" + user.ScreenName, user.Name);
          }

          text = _regex.Replace(text, "");
          if(string.IsNullOrWhiteSpace(text))
          {
            text = "I have posted a link";
          }

          if(tweet.IsRetweet)
          {
            ITweet retweeted = tweet.RetweetedTweet;

            text = string.Format("I, {0}, retweeted {1}'s tweet: {2}",
                                 tweet.CreatedBy.Name,
                                 retweeted.CreatedBy.Name,
                                 text);
          }

          await GetTweetAudioAsync(text, 
                                   discord);
        }
        catch (Exception exception)
        {    
          Exception = exception;
          _finalizeSignal.Set();
        }
        finally
        {
          await discord.FlushAsync();
        }
      }
    }
  }

  public class Program
  {
    static async Task Main(string[] args)
    {
      if(args.Length != 1)
      {
        return;
      }

      Option option = JsonConvert.DeserializeObject<Option>(File.ReadAllText(args[0]));

      TtsBot bot = new TtsBot(option);
      await bot.RunAsync();
    }
  }
}
