# Azure Cognitive Services Demo Client for TV and Devices Solutions

This project provides a demo client that helps illustrate how to build speech-enabled client solutions that leverage Azure Cognitive Services and CoPilot Studio Bots. The client builds on top of existing Microsoft sample code, pulling together details specific to delivering a successful integration for the TV and Devices solution space.

The Azure Cognitive Services used by the demo client include:

* Azure Speech-to-Text (STT)
* Azure Text-to-Speech (TTS)
* Azure Conversational Language Understanding (CLU)
* Microsoft CoPilot Studio (MCS)

## Azure Requirements

Before getting started with the demo client, you will need to have an Azure subscription and ensure the following resources are provisioned in Azure:

* Speech Resource
    * [STT](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/get-started-speech-to-text?tabs=macos%2Cterminal&pivots=ai-studio#prerequisites)
    * [TTS](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/get-started-text-to-speech?tabs=macos%2Cterminal&pivots=programming-language-csharp#prerequisites)
* [CLU](https://learn.microsoft.com/en-us/azure/ai-services/language-service/conversational-language-understanding/quickstart?pivots=language-studio)
* [CoPilot Studio Bot](https://learn.microsoft.com/en-us/microsoft-copilot-studio/fundamentals-get-started?tabs=web) *optional*

## Client Requirements

* The demo app is written in C# 8 and runs on .NET standard 2.0
* Review [Platform Requirements](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/quickstarts/setup-platform?pivots=programming-language-csharp&tabs=windows%2Cubuntu%2Cdotnetcli%2Cdotnet%2Cjre%2Cmaven%2Cnodejs%2Cmac%2Cpypi#platform-requirements) available online


## Demo Client Features

* Speech Recogntion
* Conversational Language Understanding
* Speech Recogntion + Conversational Understanding
* Text to Speech
* Audio Recorder
* Speech-Enabled Virtual Assistant

When you run the demo client, you will be presented with the following menu options:
```shell
Please select an option:
1. Recognize and Analyze
2. Recognize
3. Analyze
4. Synthesize
5. Start Conversation
6. Record Audio
7. Print Configuration
8. Exit
Enter your choice: 
```

## Usage Details

```shell
Usage: dotnet run [options]

Options:
  -h, --help        Show this usage message
  --ConfigPath      Set path to configuration file. Default: appsettings.yaml
  --LogLevel        Set log level [DEBUG, INFO, WARNING, ERROR, CRITICAL]. Default: INFO
  --KeyVaultUri     Set Azure Key Vault URI. Default: 

Recognizer Options:
  --SubscriptionKey                 Azure Speech Service subscription key. Default: YOUR_SUBSCRIPTION_KEY
  --ServiceRegion                   Azure Speech Service region. Default: eastus
  --Language                        Language code for recognition. Default: en-US
  --SourceAudioType                 Audio input type [File, Microphone]. NOTE: File not supported yet. Default: microphone
  --SourceAudioPath                 Path to audio file. Only used if SourceAudioType is File. Default: ./audio
  --ProfanityOption                 Profanity filter option [Masked, Removed, Raw]. Default: masked
  --InitialSilenceTimeoutMs         Initial silence timeout in milliseconds. Default: 10000
  --EndSilenceTimeoutMs             End silence timeout in milliseconds. Default: 1200
  --StablePartialResultThreshold    Stable partial result threshold. Default: 2

Synthesizer Options:
  --SubscriptionKey                 Azure Speech Service subscription key. Default: YOUR_SUBSCRIPTION_KEY
  --ServiceRegion                   Azure Speech Service region. Default: eastus
  --VoiceName                       Azure TTS voice name. Default: en-US-AvaMultilingualNeural
  --SpeechSynthesisOutputFormat     Azure TTS output format. Default: Raw22050Hz16BitMonoPcm
  --DestAudioType                   Audio output type [Microphone, File]. NOTE: Microphone not supported yet. Default: microphone
  --DestAudioPath                   Path to audio file. Only used if DestAudioType is File. Default: ./audio

Analyzer Options:
  --CluKey                  Azure CLU key. Default: 
  --CluResource             Azure CLU resource. Default: 
  --CluDeploymentName       Azure CLU deployment name. Default: 
  --CluProjectName          Azure CLU project name. Default: 

Bot Options:
  --BotId                       Azure Bot ID. Default: 
  --BotTenantId                 Azure Bot Tenant ID. Default: 
  --BotName                     Azure Bot Name. Default: 
  --BotTokenEndpoint            Azure Bot Token Endpoint. Default: 
  --EndConversationMessage      Message to pass to bot to signal end of conversation. Default: quit
```

## Yaml Configuration File

```yaml
App:
  KeyVaultUri: https://YOUR_AZURE_KEYVAULT.vault.azure.net
  LogLevel: INFO # DEBUG, INFO, WARNING, ERROR, CRITICAL

Bot:
  BotId: YOUR_BOT_ID
  BotTenantId: YOUR_BOT_TENANT_ID
  BotName: YOUR_BOT_NAME
  BotTokenEndpoint: https://YOUR_BOT_TOKEN_ENDPOINT
  EndConversationMessage: quit

Recognizer: 
  SubscriptionKey: YOUR_AZURE_SPEECH_KEY
  ServiceRegion: eastus
  Language: en-US
  SourceAudioType: microphone # microphone (file not supported)
  SourceAudioPath: ./audio # path to either a folder containing audio or a specific audio file
  ProfanityOption: masked # raw or masked
  InitialSilenceTimeoutMs: 10000 # in milliseconds
  EndSilenceTimeoutMs: 1200 # in milliseconds
  StablePartialResultThreshold: 2

Synthesizer: 
  SubscriptionKey: YOUR_AZURE_SPEECH_KEY
  ServiceRegion: eastus
  VoiceName: en-US-AvaMultilingualNeural
  SpeechSynthesisOutputFormat: Raw22050Hz16BitMonoPcm
  DestAudioType: file # file (microphone not supported)
  DestAudioPath: ./audio # path to either a folder containing audio or a specific audio file

Analyzer:
  CluKey: YOUR_CLU_KEY
  CluResource: YOUR_CLU_RESOURCE
  CluDeploymentName: YOUR_CLU_DEPLOYMENT_NAME
  CluProjectName: YOUR_CLU_PROJECT_NAME
```

## Environment Vars

All command-line args can be passed in as an environment var. 

Environment var naming convention is `AILDEMO_\<cli option name\>`

Example: AzureKeyVaultUri --> AILDEMO_AzureKeyVaultUri

> **Note**: Environment Var support is case-*in*sensitive. So `AILDEMO_AzureKeyVaultUri` and `AILDEMO_AZUREKEYVAULTURI` are both equivalent.

## Azure Key Vault

The demo client supports integration with Azure Key Vault as a storage option for sensitive arguments. To leverage Azure Key Vault:

* Create an Azure Key Vault Resource
* Create a Key Vault Secret using the same name as the command-line arg
* log in to Azure at the command line ('az login')
* Provide your `KeyVaultUri` via command-line arg, yaml config, or env var

## Argument Parsing Precedence

The demo client applies settings using the following precedence:

1. Key Vault
2. Command line
3. Environment variables
4. YAML file

For example, a value stored in Key Vault takes precedence over a value passed in via the CLI. And any value provided in the yaml file will be over-written if also specified in one of the other available options.


## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
