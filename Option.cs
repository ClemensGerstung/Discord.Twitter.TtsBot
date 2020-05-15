﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Discord.Twitter.TtsBot
{
  [JsonConverter(typeof(OptionConverter))]
  public class Option
  {
    [JsonProperty("auth.twitter.consumerKey")]
    public string TwitterConsumerKey { get; set; }
    
    [JsonProperty("auth.twitter.consumerSecret")]
    public string TwitterConsumerSecret { get; set; }
    
    [JsonProperty("auth.twitter.userAccessToken")]
    public string TwitterUserAccessToken { get; set; }

    [JsonProperty("auth.twitter.userAccessSecret")]
    public string TwitterUserAccessSecret { get; set; }

    [JsonProperty("auth.google.setEnvironmentVariable")]
    public bool GoogleSetEnvironmentVariable { get; set; }

    [JsonProperty("auth.google.applicationCredentials")]
    public string GoogleApplicationCredentialsPath { get; set; }

    [JsonProperty("auth.discord.token")]
    public string DiscordToken { get; set; }

    [JsonProperty("data.follow")]
    public string FollowTwitterUser { get; set; }


    [JsonProperty("data.voiceName")]
    public string VoiceName { get; set; }

    [JsonProperty("data.commandPrefix")]
    public string CommandPrefix { get; set; }
  }
}
