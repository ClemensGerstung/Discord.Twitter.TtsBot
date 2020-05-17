# Discord.Twitter.TtsBot

A simple bot which checks for new tweets of a user, uses Google Cloud TTS to say the tweet within discord.  
Uses .NET Core so it should be able to run everywhere (where .NET Core is supported).  

## Configuration/Build

Don't forget to check the `.csproj` file and edit if necessary and set the `RuntimeIdentifier`. 
> For valid identifiers check Microsoft's documentation: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog

For instance when running on a Pi add this:
```xml
<PropertyGroup>
  <RuntimeIdentifier>linux-arm</RuntimeIdentifier>
</PropertyGroup>
```

Alternatively you can provide the `-r` when calling `dotnet build`.

## Run

Create a `config.json` file which must be supplied as the first and only parameter to the bot. 
```cmd
./Discord.Twitter.TtsBot config.json
```

The `config.json` must look like this:
```config.json
{
  "auth": {
    "twitter": {
      "consumerKey": "<>",
      "consumerSecret": "<>",
      "userAccessToken": "<>",
      "userAccessSecret": "<>"
    },
    "google": {
      "setEnvironmentVariable": true,
      "applicationCredentials": "<>"
    },
    "discord": {
      "token": "<>"
    }
  },
  "data": {
    "follow": "<>",
    "voiceName": "<>",
    "commandPrefix": "<>"
  }
}
```

* `auth.twitter.consumerKey`: Twitter Consumer Key which can be obtained from the Twitter API website
* `auth.twitter.consumerSecret`: Twitter Consumer Secret which can be obtained from the Twitter API website
* `auth.twitter.userAccessToken`: Twitter User Access Token which can be obtained from the Twitter API website
* `auth.twitter.userAccessSecret`: Twitter User Access Secret which can be obtained from the Twitter API website
* `auth.google.setEnvironmentVariable"`: Flag to set the environment variable `GOOGLE_APPLICATION_CREDENTIALS` to link to the Google Cloud Service Account json authentication file
* `auth.google.applicationCredentials`: Path to the Google Cloud Service Account json authentication file, can be obtained on the Google Cloud Console (check documentation)
* `auth.discord.token`: Discord Bot Token which can be obtained from the Discord Developer Portal
* `data.follow`: Screenname of the Twitter user to follow (i.e. the name without `@` symbol)
* `data.voiceName`: Name of the voice to synthesize the text (use `TextToSpeechClient::ListVoices` to get all available voices)
* `data.commandPrefix`: text on which the bot listens (i.e. `!ttsBot`)
