using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Text.Json;
using SpeechEnabledTvClient.Models;
using SpeechEnabledTvClient.Monitoring;
using SpeechEnabledTvClient.Services.Analyzer;
using SpeechEnabledTvClient.Services.Bot;
using SpeechEnabledTvClient.Services.Recognizer;
using SpeechEnabledTvClient.Services.Synthesizer;

namespace SpeechEnabledTvClient 
{
    public class Program
    {
        public readonly ILogger logger;
        public readonly AppSettings settings;

        // Cache history of CLI inputs for quick up/down arrow navigation
        private static List<string> mainMenuHistory = new List<string>();
        private static List<string> analyzerHistory = new List<string>();
        private static List<string> synthesizerHistory = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="settings">The application settings.</param>
        public Program(AppSettings settings) {
            this.settings = settings;
            
            try
            {
                // Create a logger that includes logging to file, console, and optionally Azure Monitor
                ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddFile("Logs/myapp-{Date}.txt");
                    // builder.AddJsonConsole();    // Uncomment to enable JSON console logging
                    builder.AddSimpleConsole(o => {
                            o.TimestampFormat = "HH:mm:ss.fff ";
                            o.IncludeScopes = true;
                            o.SingleLine = true;
                        });

                    if (!string.IsNullOrEmpty(settings.AppInsightsConnectionString))
                    {
                        builder.AddOpenTelemetry(logging =>
                        {
                            logging.AddAzureMonitorLogExporter(options =>
                            {
                                options.ConnectionString = settings.AppInsightsConnectionString;
                            });
                        });
                    }
                });

                logger = loggerFactory.CreateLogger("DTV.Demo");
            }
            catch (System.Exception ex)
            {
                // If logging fails, log to file and console
                System.Console.WriteLine($"Error initializing logging: {ex.Message}");
                
                ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddFile("Logs/myapp-{Date}.txt");
                    builder.AddSimpleConsole(o => {
                            o.TimestampFormat = "HH:mm:ss.fff ";
                            o.IncludeScopes = true;
                            o.SingleLine = true;
                        });
                });
                logger = loggerFactory.CreateLogger("DTV.Demo");
            }
        }

        // ASR + NLU
        public void RecognizeAndAnalyze()
        {
            Recognizer recognizer = new Recognizer(logger, new SpeechEnabledTvClient.Monitoring.Monitor(settings));
            Analyzer analyzer = new Analyzer(logger, new SpeechEnabledTvClient.Monitoring.Monitor(settings));

            string input = string.Empty;
            while (input != "quit")
            {
                Console.Write("Press enter to start recognition (type 'quit' to exit): ");
                input = ReadLine.Read();
                if (input == "quit")
                {
                    break;
                }
                recognizer.Recognize(analyzer, null).Wait();
            }
        }

        // ASR only
        public void Recognize()
        {
            Recognizer recognizer = new Recognizer(logger, new SpeechEnabledTvClient.Monitoring.Monitor(settings));
            
            string input = string.Empty;
            while (input != "quit")
            {
                Console.Write("Press enter to start recognition (type 'quit' to exit): ");
                input = ReadLine.Read();
                if (input == "quit")
                {
                    break;
                }
                recognizer.Recognize(null, null).Wait();
            }
        }

        // NLU only
        public void Analyze()
        {
            Analyzer analyzer = new Analyzer(logger, new SpeechEnabledTvClient.Monitoring.Monitor(settings));

            string input = string.Empty;
            restoreHistory(analyzerHistory);

            while (input != "quit")
            {
                Console.Write("Enter text for analysis (type 'quit' to exit): ");
                input = ReadLine.Read();
                if (input == "quit")
                {
                    break;
                }
                AnalyzerResponse response = analyzer.Analyze(input);
                if (response != null && !response.IsError) {
                    Interpretation interpretation = response.interpretation;
                    Prediction prediction = interpretation.result.prediction;
                    logger.LogInformation($"Analysis result: ");
                    logger.LogInformation($"\tIntent: {prediction.topIntent} ({prediction.intents[0].confidenceScore})");

                    logger.LogInformation($"\tEntities:");
                    foreach (Entity entity in prediction.entities)
                    {
                        logger.LogInformation($"\t\tCategory: {entity.category}");
                        logger.LogInformation($"\t\t\tText: {entity.text}");
                        logger.LogInformation($"\t\t\tValue: {entity.value}");
                        logger.LogInformation($"\t\t\tOffset: {entity.offset}");
                        logger.LogInformation($"\t\t\tLength: {entity.length}");
                        logger.LogInformation($"\t\t\tConfidence: {entity.confidenceScore}");

                        if (entity.nuance_CALENDARX != null)
                        {
                            logger.LogInformation($"\t\t\tCalendarX: {JsonSerializer.Serialize(entity.nuance_CALENDARX)}");
                        }
                    }

                }
                else if (response != null && response.HasErrorResponse) {
                    ErrorResponse error = response.error;
                    logger.LogError($"Error: {error.content.error.message}");
                }
                else {
                    logger.LogError($"Error: could not parse response: {JsonSerializer.Serialize(response)}");
                }
                System.Threading.Thread.Sleep(500); // Provide a slight delay so logs are not interleaved with console output
            }

            stashHistory(ref analyzerHistory);
        }

        // TTS
        public void Synthesize()
        {
            Synthesizer synthesizer = new Synthesizer(logger, new SpeechEnabledTvClient.Monitoring.Monitor(settings));

            string input = string.Empty;
            restoreHistory(synthesizerHistory);

            while (input != "quit")
            {
                Console.Write("Enter text for synthesis (type 'quit' to exit): ");
                input = ReadLine.Read();
                if (input == "quit")
                {
                    break;
                }
                synthesizer.Synthesize(input).Wait();
            }

            stashHistory(ref synthesizerHistory);
        }

        // CoPilot Studio Bot
        public void StartConversation()
        {
            Recognizer recognizer = new Recognizer(logger, new SpeechEnabledTvClient.Monitoring.Monitor(settings));
            CopilotClient client = new CopilotClient(logger);
            client.StartConversation(recognizer).Wait();
        }

        // Audio Recorder
        public void RecordAudio()
        {
            Recorder recorder = new Recorder(logger);
            recorder.RecordAudio();
        }

        // Main menu
        static void showMenu()
        {
            Console.WriteLine("Please select an option:");
            Console.WriteLine("1. Recognize and Analyze");
            Console.WriteLine("2. Recognize");
            Console.WriteLine("3. Analyze");
            Console.WriteLine("4. Synthesize");
            Console.WriteLine("5. Start Conversation");
            Console.WriteLine("6. Record Audio");
            Console.WriteLine("7. Print Configuration");
            Console.WriteLine("8. Exit");
        }

        // CLI Usage
        static void showUsage()
        {
            Console.WriteLine("Usage: dotnet run [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help                        Show this usage message");
            Console.WriteLine($"  --ConfigPath                      Set path to configuration file. Default: {AppSettings.Defaults.ConfigPath}");
            Console.WriteLine($"  --LogLevel                        Set log level [DEBUG, INFO, WARNING, ERROR, CRITICAL]. Default: {AppSettings.Defaults.LogLevel}");
            Console.WriteLine($"  --AppInsightsConnectionString     Set Azure App Insights connection string. Default: {AppSettings.Defaults.AppInsightsConnectionString}");
            Console.WriteLine($"  --KeyVaultUri                     Set Azure Key Vault URI. Default: {AppSettings.Defaults.KeyVaultUri}");
            Console.WriteLine();
            Console.WriteLine("Recognizer Options:");
            Console.WriteLine($"  --SubscriptionKey                 Azure Speech Service subscription key. Default: {RecognizerSettings.Defaults.SubscriptionKey}");
            Console.WriteLine($"  --ServiceRegion                   Azure Speech Service region. Default: {RecognizerSettings.Defaults.ServiceRegion}");
            Console.WriteLine($"  --Language                        Language code for recognition. Default: {RecognizerSettings.Defaults.Language}");
            Console.WriteLine($"  --SourceAudioType                 Audio input type [file, microphone]. NOTE: File not supported yet. Default: {RecognizerSettings.Defaults.SourceAudioType}");
            Console.WriteLine($"  --SourceAudioPath                 Path to audio file. Only used if SourceAudioType is File. Default: {RecognizerSettings.Defaults.SourceAudioPath}");
            Console.WriteLine($"  --ProfanityOption                 Profanity filter option [Masked, Removed, Raw]. Default: {RecognizerSettings.Defaults.ProfanityOption}");
            Console.WriteLine($"  --InitialSilenceTimeoutMs         Initial silence timeout in milliseconds. Default: {RecognizerSettings.Defaults.InitialSilenceTimeoutMs}");
            Console.WriteLine($"  --EndSilenceTimeoutMs             End silence timeout in milliseconds. Default: {RecognizerSettings.Defaults.EndSilenceTimeoutMs}");
            Console.WriteLine($"  --ListeningTimeoutMs              Listening timeout in milliseconds if start of speech is not detected. Set to 0 to disable. Default: {RecognizerSettings.Defaults.ListeningTimeoutMs}"); 
            Console.WriteLine($"  --RecognitionTimeoutMs            Recognition timeout in milliseconds. Default: {RecognizerSettings.Defaults.RecognitionTimeoutMs}");
            Console.WriteLine($"  --StablePartialResultThreshold    Stable partial result threshold. Default: {RecognizerSettings.Defaults.StablePartialResultThreshold}");
            Console.WriteLine($"  --CaptureAudio                    Enable to capture audio to file for debug. Default: {RecognizerSettings.Defaults.CaptureAudio}");
            Console.WriteLine();
            Console.WriteLine("Synthesizer Options:");
            Console.WriteLine($"  --SubscriptionKey                 Azure Speech Service subscription key. Default: {SynthesizerSettings.Defaults.SubscriptionKey}");
            Console.WriteLine($"  --ServiceRegion                   Azure Speech Service region. Default: {SynthesizerSettings.Defaults.ServiceRegion}");
            Console.WriteLine($"  --VoiceName                       Azure TTS voice name. Default: {SynthesizerSettings.Defaults.VoiceName}");
            Console.WriteLine($"  --SpeechSynthesisOutputFormat     Azure TTS output format. Default: {SynthesizerSettings.Defaults.SpeechSynthesisOutputFormat}");
            Console.WriteLine($"  --DestAudioType                   Audio output type [speaker, file]. Default: {SynthesizerSettings.Defaults.DestAudioType}");
            Console.WriteLine($"  --DestAudioPath                   Path to audio file. Only used if DestAudioType is File. Default: {SynthesizerSettings.Defaults.DestAudioPath}");
            Console.WriteLine();
            Console.WriteLine("Analyzer Options:");
            Console.WriteLine($"  --CluKey                      Azure CLU key. Default: {AnalyzerSettings.Defaults.CluKey}");
            Console.WriteLine($"  --CluResource                 Azure CLU resource. Default: {AnalyzerSettings.Defaults.CluResource}");
            Console.WriteLine($"  --CluDeploymentName           Azure CLU deployment name. Default: {AnalyzerSettings.Defaults.CluDeploymentName}");
            Console.WriteLine($"  --CluProjectName              Azure CLU project name. Default: {AnalyzerSettings.Defaults.CluProjectName}");
            Console.WriteLine($"  --Enable2ndPassCompletion     Enable 2nd pass prompt completions for entity analysis using Azure AI. Default: {AnalyzerSettings.Defaults.Enable2ndPassCompletion}");
            Console.WriteLine($"  --AzureAiKey                  Azure AI API Key. Default: {AnalyzerSettings.Defaults.AzureAIKey}");
            Console.WriteLine($"  --AzureAIEndpoint             Azure AI Endpoint. Default: {AnalyzerSettings.Defaults.AzureAIEndpoint}");
            Console.WriteLine($"  --PromptDir                   Folder containing prompts. Default: {AnalyzerSettings.Defaults.PromptDir}");
            Console.WriteLine($"  --AzureStorageTableUri        Azure Storage Table Uri of resource containing entity literal/value mappings. Default: {AnalyzerSettings.Defaults.AzureStorageTableUri}");
            Console.WriteLine();
            Console.WriteLine("Bot Options:");
            Console.WriteLine($"  --BotId                       Azure Bot ID. Default: {BotSettings.Defaults.BotId}");
            Console.WriteLine($"  --BotTenantId                 Azure Bot Tenant ID. Default: {BotSettings.Defaults.BotTenantId}");
            Console.WriteLine($"  --BotName                     Azure Bot Name. Default: {BotSettings.Defaults.BotName}");
            Console.WriteLine($"  --BotTokenEndpoint            Azure Bot Token Endpoint. Default: {BotSettings.Defaults.BotTokenEndpoint}");
            Console.WriteLine($"  --EndConversationMessage      Message to pass to bot to signal end of conversation. Default: {BotSettings.Defaults.EndConversationMessage}");
            Console.WriteLine();
            Console.WriteLine("Endpointer Options:");
            Console.WriteLine($"  --StartOfSpeechWindowInMs     The amount of speech, in ms, needed to trigger start of speech. Default: {EndpointerSettings.Defaults.StartOfSpeechWindowInMs}");
            Console.WriteLine($"  --EndOfSpeechWindowInMs       The amount of trailing silence, in ms, needed to trigger end of speech. Default: {EndpointerSettings.Defaults.EndOfSpeechWindowInMs}");
        }

        // Stash ReadLine history for later restoration
        protected void stashHistory(ref List<string> history)
        {
            history = ReadLine.GetHistory();
            ReadLine.ClearHistory();
        }

        // Restore ReadLine history for given CLI context
        protected void restoreHistory(List<string> history)
        {
            ReadLine.AddHistory(history.ToArray());
        }

        // Animate a loading spinner while executing a task
        public static async Task<T> Animate<T>(string loadingMessage, Func<Task<T>> task)
        {
            var spinner = new[] { '|', '/', '-', '\\' };
            int spinnerIndex = 0;
            bool loading = true;

            var loadingTask = Task.Run(async () =>
            {
                while (loading)
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write($"{loadingMessage} {spinner[spinnerIndex]}");
                    spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                    await Task.Delay(100);
                }
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(new string(' ', Console.WindowWidth)); // Clear the spinner line
                Console.SetCursorPosition(0, Console.CursorTop);
            });

            // Execute the provided task
            T result = await task();
            loading = false;

            await loadingTask; // Ensure the loading task completes

            return result;
        }

        // Load application settings
        public static AppSettings LoadAppSettings(string[] args)
        {
            // Use a loading spinner while loading settings. Reading from Key Vault can take a few seconds.
            return Animate("Loading settings...", async () =>
            {
                return await Task.Run(() => AppSettings.LoadAppSettings(args));
            }).Result; // Blocks until the task completes and retrieves the result
        }

        // Main entry point
        static void Main(string[] args)
        {
            Console.WriteLine($"Client version: {Version.version}");
            Console.WriteLine("Welcome to the Speech-Enabled TV Client!");

            if (args.Contains("-h") || args.Contains("--help"))
            {
                showUsage();
                return;
            }

            AppSettings appSettings = LoadAppSettings(args);
            Program app = new Program(appSettings);

            showMenu();

            // Initialize CLI elements            
            ReadLine.HistoryEnabled = true;
            string input = string.Empty;

            // Main Menu Loop
            while (true)
            {
                Console.Write("Enter your choice: ");
                input = ReadLine.Read();

                switch (input)
                {
                    case "1":
                        try { app.RecognizeAndAnalyze(); }
                        catch (System.Exception e) {
                            Console.WriteLine($"Error: {e.Message}");
                            app.logger.LogError(e.Message);
                        }
                        break;
                    case "2":
                        try { app.Recognize(); }
                        catch (System.Exception e) { Console.WriteLine($"Error: {e.Message}"); }
                        break;
                    case "3":
                        app.stashHistory(ref mainMenuHistory);
                        app.Analyze();
                        app.restoreHistory(mainMenuHistory);
                        break;
                    case "4":
                        app.stashHistory(ref mainMenuHistory);
                        app.Synthesize();
                        app.restoreHistory(mainMenuHistory);
                        break;
                    case "5":
                        app.StartConversation();
                        break;
                    case "6":
                        app.RecordAudio();
                        break;
                    case "7":
                        Console.WriteLine(appSettings.ToString());
                        break;
                    case "8":
                        Console.WriteLine("Exiting...");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        showMenu();
                        break;
                }
            }
        }
    }
}