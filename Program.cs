using System.Text.Json;
using Microsoft.Extensions.Logging;
using SpeechEnabledCoPilot.Services.Analyzer;
using SpeechEnabledCoPilot.Services.Recognizer;
using SpeechEnabledCoPilot.Services.Synthesizer;
using SpeechEnabledCoPilot.Services.Bot;
using SpeechEnabledCoPilot.Models;

namespace SpeechEnabledCoPilot
{
    class Program
    {
        // Cache history of CLI inputs for quick up/down arrow navigation
        private static List<string> mainMenuHistory = new List<string>();
        private static List<string> analyzerHistory = new List<string>();
        private static List<string> synthesizerHistory = new List<string>();

        // ASR + NLU
        public void RecognizeAndAnalyze()
        {
            Recognizer recognizer = new Recognizer();
            Analyzer analyzer = new Analyzer();
            recognizer.Recognize(analyzer, null).Wait();
        }

        // ASR only
        public void Recognize()
        {
            Recognizer recognizer = new Recognizer();

            for (int i = 0; i < 3; i++) {
                Console.WriteLine($"Recognizing {i + 1} of 3...");
                recognizer.Recognize(null, null).Wait();
            }
        }

        // NLU only
        public void Analyze()
        {
            Analyzer analyzer = new Analyzer();

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
                JsonElement result = analyzer.Analyze(input);

                // Extract the prediction from the result and format prediction data
                JsonElement prediction = result.GetProperty("prediction");

                string? topIntent = prediction.GetProperty("topIntent").GetString();
                JsonElement[] intents = prediction.GetProperty("intents").EnumerateArray().ToArray();
                var confidenceScore = intents[0].GetProperty("confidenceScore").GetSingle();

                Console.WriteLine($"Analysis result: ");
                Console.WriteLine($"\tIntent: {topIntent} ({confidenceScore})");

                Console.WriteLine($"\tEntities:");
                foreach (JsonElement entity in prediction.GetProperty("entities").EnumerateArray())
                {
                    Console.WriteLine($"\t\tCategory: {entity.GetProperty("category").GetString()}");
                    Console.WriteLine($"\t\t\tText: {entity.GetProperty("text").GetString()}");
                    Console.WriteLine($"\t\t\tOffset: {entity.GetProperty("offset").GetInt32()}");
                    Console.WriteLine($"\t\t\tLength: {entity.GetProperty("length").GetInt32()}");
                    Console.WriteLine($"\t\t\tConfidence: {entity.GetProperty("confidenceScore").GetSingle()}");
                }
            }

            stashHistory(ref analyzerHistory);
        }

        // TTS
        public void Synthesize()
        {
            Synthesizer synthesizer = new Synthesizer();

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
            Recognizer recognizer = new Recognizer();
            CoPilotClient client = new CoPilotClient();
            client.StartConversation(recognizer).Wait();
        }

        // Audio Recorder
        public void RecordAudio()
        {
            Recorder recorder = new Recorder();
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
            Console.WriteLine("  -h, --help        Show this usage message");
            Console.WriteLine($"  --ConfigPath      Set path to configuration file. Default: {AppSettings.Defaults.ConfigPath}");
            Console.WriteLine($"  --LogLevel        Set log level [DEBUG, INFO, WARNING, ERROR, CRITICAL]. Default: {AppSettings.Defaults.LogLevel}");
            Console.WriteLine($"  --KeyVaultUri     Set Azure Key Vault URI. Default: {AppSettings.Defaults.KeyVaultUri}");
            Console.WriteLine();
            Console.WriteLine("Recognizer Options:");
            Console.WriteLine($"  --SubscriptionKey                 Azure Speech Service subscription key. Default: {RecognizerSettings.Defaults.SubscriptionKey}");
            Console.WriteLine($"  --ServiceRegion                   Azure Speech Service region. Default: {RecognizerSettings.Defaults.ServiceRegion}");
            Console.WriteLine($"  --Language                        Language code for recognition. Default: {RecognizerSettings.Defaults.Language}");
            Console.WriteLine($"  --SourceAudioType                 Audio input type [file, microphone]. NOTE: File not supported yet. Default: {RecognizerSettings.Defaults.SourceAudioType}");
            Console.WriteLine($"  --SourceAudioPath                 Path to audio file. Only used if SourceAudioType is File. Default: {RecognizerSettings.Defaults.SourceAudioPath}");
            Console.WriteLine($"  --ProfanityOption                 Profanity filter option [raw, masked]. Default: {RecognizerSettings.Defaults.ProfanityOption}");
            Console.WriteLine($"  --InitialSilenceTimeoutMs         Initial silence timeout in milliseconds. Default: {RecognizerSettings.Defaults.InitialSilenceTimeoutMs}");
            Console.WriteLine($"  --EndSilenceTimeoutMs             End silence timeout in milliseconds. Default: {RecognizerSettings.Defaults.EndSilenceTimeoutMs}");
            Console.WriteLine($"  --StablePartialResultThreshold    Stable partial result threshold. Default: {RecognizerSettings.Defaults.StablePartialResultThreshold}");
            Console.WriteLine();
            Console.WriteLine("Synthesizer Options:");
            Console.WriteLine($"  --SubscriptionKey                 Azure Speech Service subscription key. Default: {SynthesizerSettings.Defaults.SubscriptionKey}");
            Console.WriteLine($"  --ServiceRegion                   Azure Speech Service region. Default: {SynthesizerSettings.Defaults.ServiceRegion}");
            Console.WriteLine($"  --VoiceName                       Azure TTS voice name. Default: {SynthesizerSettings.Defaults.VoiceName}");
            Console.WriteLine($"  --SpeechSynthesisOutputFormat     Azure TTS output format. Default: {SynthesizerSettings.Defaults.SpeechSynthesisOutputFormat}");
            Console.WriteLine($"  --DestAudioType                   Audio output type [file, microphone]. NOTE: Microphone not supported yet. Default: {SynthesizerSettings.Defaults.DestAudioType}");
            Console.WriteLine($"  --DestAudioPath                   Path to audio file. Only used if DestAudioType is File. Default: {SynthesizerSettings.Defaults.DestAudioPath}");
            Console.WriteLine();
            Console.WriteLine("Analyzer Options:");
            Console.WriteLine($"  --CluKey                  Azure CLU key. Default: {AnalyzerSettings.Defaults.CluKey}");
            Console.WriteLine($"  --CluResource             Azure CLU resource. Default: {AnalyzerSettings.Defaults.CluResource}");
            Console.WriteLine($"  --CluDeploymentName       Azure CLU deployment name. Default: {AnalyzerSettings.Defaults.CluDeploymentName}");
            Console.WriteLine($"  --CluProjectName          Azure CLU project name. Default: {AnalyzerSettings.Defaults.CluProjectName}");
            Console.WriteLine();
            Console.WriteLine("Bot Options:");
            Console.WriteLine($"  --BotId                       Azure Bot ID. Default: {BotSettings.Defaults.BotId}");
            Console.WriteLine($"  --BotTenantId                 Azure Bot Tenant ID. Default: {BotSettings.Defaults.BotTenantId}");
            Console.WriteLine($"  --BotName                     Azure Bot Name. Default: {BotSettings.Defaults.BotName}");
            Console.WriteLine($"  --BotTokenEndpoint            Azure Bot Token Endpoint. Default: {BotSettings.Defaults.BotTokenEndpoint}");
            Console.WriteLine($"  --EndConversationMessage      Message to pass to bot to signal end of conversation. Default: {BotSettings.Defaults.EndConversationMessage}");
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

        // Main entry point
        static void Main(string[] args)
        {
            if (args.Contains("-h") || args.Contains("--help"))
            {
                showUsage();
                return;
            }

            Program app = new Program();
            AppSettings appSettings = AppSettings.LoadAppSettings(args);

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
                        catch (System.Exception e) { Console.WriteLine($"Error: {e.Message}"); }
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