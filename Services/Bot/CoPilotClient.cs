using SpeechEnabledTvClient .Models;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Text.Json;
using SpeechEnabledTvClient .Services.Recognizer;
using SpeechEnabledTvClient .Services.Analyzer;
using SpeechEnabledTvClient .Monitoring;

namespace SpeechEnabledTvClient .Services.Bot
{
    public class CoPilotClient : IRecognizerResponseHandler
    {
        private readonly ILogger _logger;
        private static string? _watermark = null;
        private static IBotService? _botService;
        private static BotSettings? _appSettings;
        private string _botId;
        private static string _botName;
        private string _botTenantId;
        private string _botTokenEndpoint;
        private static string? _endConversationMessage;
        private static string _userDisplayName = "You";
        private string? _userInput = "";


        public CoPilotClient(ILogger logger)
        {
            // var configuration = new ConfigurationBuilder()
            //                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            //                    .AddYamlFile("appsettings.yaml", optional: true)
            //                    .AddEnvironmentVariables()
            //                    .Build();
            _logger = logger;
            _appSettings = AppSettings.BotSettings();
            _endConversationMessage = _appSettings.EndConversationMessage ?? "quit";
            
            var serviceProvider = new ServiceCollection()
                                .AddLogging()
                                .AddSingleton<IBotService, BotService>()                
                                .BuildServiceProvider();
            if ( serviceProvider == null ) {
                throw new Exception("Service provider is null.");
            }

            IBotService? ibs = serviceProvider.GetService<IBotService>();
            if ( ibs == null ) {
                throw new Exception("IBotService is null.");
            }
            _botService = ibs;

            if (string.IsNullOrEmpty(_appSettings.BotId) || 
                string.IsNullOrEmpty(_appSettings.BotTenantId) || 
                string.IsNullOrEmpty(_appSettings.BotTokenEndpoint) || 
                string.IsNullOrEmpty(_appSettings.BotName))
            {
                Console.WriteLine("Update appsettings and start again.");
                Console.WriteLine("Press any key to exit");
                Console.Read();
                Environment.Exit(0);
            }
            
            _botName = _appSettings.BotName;
            _botId = _appSettings.BotId;
            _botTenantId = _appSettings.BotTenantId;
            _botTokenEndpoint = _appSettings.BotTokenEndpoint;

            Console.WriteLine($"To end the conversation, simply say '{_appSettings.EndConversationMessage}'");
        }

        public static List<Activity> WaitForResponse(DirectLineClient directLineClient, string conversationId)
        {
            return Program.Animate("Thinking...", async () =>
            {
                // Simulate loading app settings (replace this with your actual logic)
                return await Task.Run(() => GetBotResponseActivitiesAsync(directLineClient, conversationId));
            }).Result; // Blocks until the task completes and retrieves the result
        }


        public async Task StartConversation(SpeechEnabledTvClient .Services.Recognizer.Recognizer? recognizer)
        {
            if (_botService == null)
            {
                throw new Exception("BotService is null.");
            }

            if (_botService == null || _appSettings == null)
            {
                throw new Exception("BotService or AppSettings is null.");
            }

            var directLineToken = await _botService.GetTokenAsync(_botTokenEndpoint);
            using (var directLineClient = new DirectLineClient(directLineToken.Token))
            {
                var conversation = await directLineClient.Conversations.StartConversationAsync();
                var conversationId = conversation.ConversationId;
                string inputMessage;
                _logger.LogInformation($"[{conversation.ConversationId}] Client Start Copilot Conversation");

                while (true)
                {
                    if (recognizer != null) {
                        inputMessage = GetUserInput(recognizer);
                    } else {
                        inputMessage = GetUserInput();
                    }
                    Console.WriteLine($"Input Message = {inputMessage}");
                    if (string.Equals(inputMessage.TrimEnd('.'), _endConversationMessage, StringComparison.OrdinalIgnoreCase))
                    {
                        directLineClient.Conversations.PostActivityAsync(conversationId, new Activity()
                        {
                            Type = ActivityTypes.EndOfConversation,
                            From = new ChannelAccount { Id = "userId", Name = "userName" },
                            Locale = "en-Us",
                        }).Wait();
                        directLineClient.Dispose();
                        _watermark = null;
                        _logger.LogInformation($"[{conversation.ConversationId}] Client Copilot Conversation Ended");
                        Console.WriteLine("Goodbye!");
                        break;
                    }

                    // Send user message using directlineClient
                    await directLineClient.Conversations.PostActivityAsync(conversationId, new Activity()
                    {
                        Type = ActivityTypes.Message,
                        From = new ChannelAccount { Id = "userId", Name = "userName" },
                        Text = inputMessage,
                        TextFormat = "plain",
                        Locale = "en-Us",
                    });

                    Console.WriteLine($"{_botName}:");

                    // Get bot response using directlineClient
                    int count = 0;
                    _userInput = "";
                    List<Activity> responses = new List<Activity>();
                    while (count <= 0) {
                        responses = WaitForResponse(directLineClient, conversationId);
                        count = responses.Count;
                    }
                    BotReply(responses);
                    Console.WriteLine("Done replying.");
                    Thread.Sleep(500);
                }
            }
        }


        /// <summary>
        /// Use directlineClient to get bot response
        /// </summary>
        /// <returns>List of DirectLine activities</returns>
        /// <param name="directLineClient">directline client</param>
        /// <param name="conversationId">current conversation ID</param>
        /// <param name="botName">name of bot to connect to</param>// <summary>        
        private static async Task<List<Activity>> GetBotResponseActivitiesAsync(DirectLineClient directLineClient, string conversationId)
        {
            ActivitySet? response = null;
            List<Activity>? result = new List<Activity>();

            do
            {
                response = await directLineClient.Conversations.GetActivitiesAsync(conversationId, _watermark);
                if (response == null)
                {
                    // response can be null if directLineClient token expires
                    Console.WriteLine("Conversation expired. Press any key to exit.");
                    Console.Read();
                    directLineClient.Dispose();
                    Environment.Exit(0);
                }

                _watermark = response?.Watermark;
                result = response?.Activities?.Where(x =>
                  x.Type == ActivityTypes.Message &&
                    string.Equals(x.From.Name, _botName, StringComparison.Ordinal)).ToList();

                if (result != null && result.Any())
                {
                    return result;
                }

                Thread.Sleep(1000);
            } while (response != null && response.Activities.Any());

            return new List<Activity>();
        }

        /// <summary>
        /// Prompt for user input
        /// </summary>
        /// <returns>user message as string</returns>
        private string GetUserInput()
        {
            Console.WriteLine($"{_userDisplayName}:");
            _userInput = Console.ReadLine();
            return (_userInput == null) ? "" : _userInput;
        }

        private string GetUserInput(SpeechEnabledTvClient .Services.Recognizer.Recognizer recognizer) {
            // _userInput = "";
            string[] phrases = new string[] { "SPIDER-MAN" };

            while (string.IsNullOrEmpty(_userInput)) {
                recognizer.Recognize(this, phrases).Wait();
            }
                return _userInput;
        }


        /// <summary>
        /// Print bot reply to console
        /// </summary>
        /// <param name="responses">List of DirectLine activities <see cref="https://github.com/Microsoft/botframework-sdk/blob/master/specs/botframework-activity/botframework-activity.md"/>
        /// </param>
        private void BotReply(List<Activity> responses)
        {
            responses?.ForEach(responseActivity =>
            {
                // responseActivity is standard Microsoft.Bot.Connector.DirectLine.Activity
                // See https://github.com/Microsoft/botframework-sdk/blob/master/specs/botframework-activity/botframework-activity.md for reference
                // Showing examples of Text & SuggestedActions in response payload
                if (!string.IsNullOrEmpty(responseActivity.Text))
                {
                    Console.WriteLine(string.Join(Environment.NewLine, responseActivity.Text));
                }

                if (responseActivity.SuggestedActions != null && responseActivity.SuggestedActions.Actions != null)
                {
                    var options = responseActivity.SuggestedActions?.Actions?.Select(a => a.Title).ToList();
                    if (options != null) {
                        Console.WriteLine($"\t{string.Join(" | ", options)}");
                    }
                }
            });
        }

        public void onRecognitionStarted(string sessionId)
        {
            // Console.WriteLine($"Recognition started: {sessionId}");
            Console.WriteLine("Listening...");
            _userInput = "";
        }

        public void onRecognitionComplete(string sessionId)
        {
            // Console.WriteLine($"Recognition complete: {sessionId}");
        }

        public void onSpeechStartDetected(string sessionId, long offset) {
            // Console.WriteLine($"Speech start detected: {sessionId} at {offset}");
            Console.WriteLine("Processing audio...");
        }

        public void onSpeechEndDetected(string sessionId, long offset) {
            // Console.WriteLine($"Speech end detected: {sessionId} at {offset}");
            Console.WriteLine("done.");
        }

        public void onRecognitionResult(string sessionId, long offset, SpeechRecognitionResult result) {
            // Console.WriteLine($"Recognition result: {sessionId} at {offset} with result: {result.Text}");
        }

        public void onFinalRecognitionResult(string sessionId, long offset, System.Collections.Generic.IEnumerable<DetailedSpeechRecognitionResult> results) {
            // Console.WriteLine("Received final recognition result");
            _userInput = results.First().Text;
            Console.WriteLine($"{_userDisplayName}: {_userInput}");
        }

        public void onRecognitionNoMatch(string sessionId, long offset, string reason, SpeechRecognitionResult result) {
            Console.WriteLine($"Recognition no match: {sessionId} at {offset} with reason: {reason}");
        }

        public void onRecognitionCancelled(string sessionId, long offset, CancellationDetails details) {
            Console.WriteLine($"Recognition cancelled: {sessionId} at {offset} with details: {details.Reason.ToString()}");
        }
        
        public void onRecognitionError(string sessionId, string error, string details) {
            Console.WriteLine($"Recognition error: {sessionId} with error: {error} and details: {details}");
        }

        public void onAnalysisResult(string sessionId, AnalyzerResponse result) {
            Console.WriteLine($"Analysis result: {JsonSerializer.Serialize(result)}");
        }

        public void onAnalysisError(string sessionId, string error, string details) {
            Console.WriteLine($"Analysis error: {error} with details: {details}");
        }
    }
}