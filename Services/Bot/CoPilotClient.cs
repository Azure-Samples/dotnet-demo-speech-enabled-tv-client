using SpeechEnabledTvClient.Models;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Text.Json;
using SpeechEnabledTvClient.Services.Recognizer;
using SpeechEnabledTvClient.Services.Analyzer;
using SpeechEnabledTvClient.Monitoring;

namespace SpeechEnabledTvClient.Services.Bot
{
    public class CopilotClient : IRecognizerResponseHandler
    {
        private readonly ILogger _logger;
        private static string? _watermark = null;
        private static IBotService? _botService;
        private static BotSettings? _appSettings;
        private string _botId;
        private static string _botName = string.Empty;
        private string _botTenantId;
        private string _botTokenEndpoint;
        private static string? _endConversationMessage;
        private static string _userDisplayName = "You";
        private string? _userInput = "";
        private static bool _isListening = false;


        public CopilotClient(ILogger logger)
        {
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

            CopilotSays($"To end the conversation, simply say '{_appSettings.EndConversationMessage}'");
        }

        private void CopilotSays(string message) {
            Console.WriteLine($"{_botName}: {message}");
        }

        private void UserSays(string message) {
            Console.WriteLine($"{_userDisplayName}: {message}");
        }

        public static List<Activity> WaitForResponse(DirectLineClient directLineClient, string conversationId)
        {
            return Program.Animate($"{_botName}: Thinking...", async () =>
            {
                return await Task.Run(() => GetBotResponseActivitiesAsync(directLineClient, conversationId));
            }).Result; // Blocks until the task completes and retrieves the result
        }


        public async Task StartConversation(SpeechEnabledTvClient.Services.Recognizer.Recognizer? recognizer)
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
                        _isListening = false;
                        inputMessage = GetUserInput(recognizer);
                    } else {
                        inputMessage = GetUserInput();
                    }
                    CopilotSays($"Processing input: {inputMessage}");
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
                        CopilotSays("Goodbye!");
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

                    CopilotSays($"{_botName}: Here's what I found...");

                    // Get bot response using directlineClient
                    int count = 0;
                    _userInput = "";
                    List<Activity> responses = new List<Activity>();
                    while (count <= 0) {
                        responses = WaitForResponse(directLineClient, conversationId);
                        count = responses.Count;
                    }
                    BotReply(responses);
                    CopilotSays("Ready for your next request!");
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

        private string GetUserInput(SpeechEnabledTvClient.Services.Recognizer.Recognizer recognizer) {
            // string[] phrases = new string[] { "SPIDER-MAN" };
            string[]? phrases = null;

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
            if (!_isListening) {
                _isListening = true;
                CopilotSays("I'm listening...");
                _userInput = "";
            }
        }

        public void onRecognitionComplete(string sessionId)
        {
            // Not implemented
            Console.WriteLine("Recognition complete");
        }

        public void onSpeechStartDetected(string sessionId, long offset) {
            CopilotSays("Processing audio...");
        }

        public void onClientSideSpeechStartDetected(string sessionId, long offset) {
            // Not implemented
            Console.WriteLine("Client side speech start detected");
        }

        public void onClientSideSpeechEndDetected(string sessionId, long offset) {
            // Not implemented
            Console.WriteLine("Client side speech end detected");
        }

        public void onRecognitionTimerExpired(string sessionId, DateTime signalTime) {
            // Not implemented
            Console.WriteLine($"Recognition timer expired: {signalTime:G}");
        }

        public void onSpeechEndDetected(string sessionId, long offset) {
            // Not implemented
            Console.WriteLine("Speech end detected");
        }

        public void onRecognitionResult(string sessionId, long offset, SpeechRecognitionResult result) {
            // Console.WriteLine($"Recognition result: {sessionId} at {offset} with result: {result.Text}");
        }

        public void onFinalRecognitionResult(string sessionId, long offset, System.Collections.Generic.IEnumerable<DetailedSpeechRecognitionResult> results) {
            _userInput = results.First().Text;
            if (!string.IsNullOrEmpty(_userInput)) {
                UserSays(_userInput);
            }
        }

        public void onRecognitionNoMatch(string sessionId, long offset, string reason, SpeechRecognitionResult result) {
            CopilotSays("I didn't catch that.  Can you repeat?");
        }

        public void onRecognitionCancelled(string sessionId, long offset, CancellationDetails details) {
            CopilotSays($"I got the following error while processing your request: {details.Reason.ToString()}");
        }
        
        public void onRecognitionError(string sessionId, string error, string details) {
            CopilotSays($"I got the following error while processing your request: {error} with details: {details}");
        }

        public void onAnalysisResult(string sessionId, AnalyzerResponse result) {
            // Not implemented
        }

        public void onAnalysisError(string sessionId, string error, string details) {
            // Not implemented
        }
    }
}