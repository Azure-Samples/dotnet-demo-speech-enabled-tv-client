using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Azure.Identity;

namespace SpeechEnabledCoPilot.Models
{
    
    /// <summary>
    /// Represents the application settings for the speech-enabled copilot.
    /// </summary>
    public class AppSettings
    {
        public class Defaults {
            public const string ConfigPath = "appsettings.yaml";
            public const string LogLevel = "INFO";
            public const string KeyVaultUri = null;
            public const string AppInsightsConnectionString = null;
        }

        private static readonly string ENV_PREFIX = "AILDEMO_";
        private static object lockObject = new object();

        private static AppSettings? _instance = null;

        public string ConfigPath { get; set; } = Defaults.ConfigPath;
        public string LogLevel { get; set; } = Defaults.LogLevel;
        public string? KeyVaultUri { get; set; } = Defaults.KeyVaultUri;
        public string? AppInsightsConnectionString { get; set; } = Defaults.AppInsightsConnectionString;

        public RecognizerSettings recognizerSettings { get; set; } = new RecognizerSettings();
        public SynthesizerSettings synthesizerSettings { get; set; } = new SynthesizerSettings();
        public AnalyzerSettings analyzerSettings { get; set; } = new AnalyzerSettings();
        public BotSettings botSettings { get; set; } = new BotSettings();

        /// <summary>
        /// Loads the application settings from various sources and returns an instance of the <see cref="AppSettings"/> class.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>An instance of the <see cref="AppSettings"/> class.</returns>
        public static AppSettings LoadAppSettings(string[] args)
        {
            lock (lockObject)
            {
                if (_instance == null)
                {
                    _instance = new AppSettings();

                    /// Do a first pass to get top-level app settings
                    IConfigurationBuilder builder = new ConfigurationBuilder()
                        .AddEnvironmentVariables(prefix: ENV_PREFIX)
                        .AddCommandLine(args);

                    var configuration = builder.Build();
                    configuration.Bind(_instance);

                    /// Do a second pass to get the rest of the settings
                    builder = new ConfigurationBuilder()
                        .AddYamlFile(_instance.ConfigPath, optional: true)
                        .AddEnvironmentVariables(prefix: ENV_PREFIX)
                        .AddCommandLine(args);

                    configuration = builder.Build();

                    /// Check if there is a section named "App" and use it if it exists.
                    /// Precedence: Command line > Environment variables > YAML file
                    var children = configuration.GetChildren();
                    if (children.Any(c => c.Key == "App"))
                    {
                        _instance = configuration.GetSection("App").Get<AppSettings>();
                    }
                    configuration.Bind(_instance);
                    if (_instance == null)
                    {
                        throw new Exception("AppSettings not loaded. Please verify your settings.");
                    }

                    /// Do a third pass to include settings stored in KeyVault
                    /// Precedence: KeyVault > Command line > Environment variables > YAML file
                    if (!string.IsNullOrEmpty(_instance.KeyVaultUri))
                    {
                        builder.AddAzureKeyVault(new Uri(_instance.KeyVaultUri), new DefaultAzureCredential());
                        configuration = builder.Build();
                        configuration.Bind(_instance);
                    }

                    /// Now that all possible settings are loaded, bind them to the appropriate objects
                    foreach (var child in children)
                    {
                        switch (child.Key)
                        {
                            case "Recognizer":
                                RecognizerSettings? rSettings = configuration.GetSection("Recognizer").Get<RecognizerSettings>();
                                if (rSettings != null)
                                {
                                    _instance.recognizerSettings = rSettings;
                                }
                                break;
                            case "Synthesizer":
                                SynthesizerSettings? sSettings = configuration.GetSection("Synthesizer").Get<SynthesizerSettings>();
                                if (sSettings != null)
                                {
                                    _instance.synthesizerSettings = sSettings;
                                }
                                break;
                            case "Analyzer":
                                AnalyzerSettings? aSettings = configuration.GetSection("Analyzer").Get<AnalyzerSettings>();
                                if (aSettings != null)
                                {
                                    _instance.analyzerSettings = aSettings;
                                }
                                break;
                            case "Bot":
                                BotSettings? bSettings = configuration.GetSection("Bot").Get<BotSettings>();
                                if (bSettings != null)
                                {
                                    _instance.botSettings = bSettings;
                                }
                                break;
                            default:
                                /// Handle unrecognized child keys if needed
                                break;
                        }
                    }
                    configuration.Bind(_instance.recognizerSettings);
                    configuration.Bind(_instance.synthesizerSettings);
                    configuration.Bind(_instance.analyzerSettings);
                    configuration.Bind(_instance.botSettings);
                }
            }

            return _instance;
        }

        public static RecognizerSettings RecognizerSettings()
        {
            if (_instance == null)
            {
                throw new Exception("AppSettings not loaded. Please call LoadAppSettings first.");
            }
            return _instance.recognizerSettings;
        }
        
        public static SynthesizerSettings SynthesizerSettings()
        {
            if (_instance == null)
            {
                throw new Exception("AppSettings not loaded. Please call LoadAppSettings first.");
            }
            return _instance.synthesizerSettings;
        }
        
        public static AnalyzerSettings AnalyzerSettings()
        {
            if (_instance == null)
            {
                throw new Exception("AppSettings not loaded. Please call LoadAppSettings first.");
            }
            return _instance.analyzerSettings;
        }
        
        public static BotSettings BotSettings()
        {
            if (_instance == null)
            {
                throw new Exception("AppSettings not loaded. Please call LoadAppSettings first.");
            }
            return _instance.botSettings;
        }

        /// <summary>
        /// Converts the current instance of the <see cref="AppSettings"/> class to its equivalent JSON string representation.
        /// </summary>
        /// <returns>A string that represents the current instance of the <see cref="AppSettings"/> class.</returns>    }
        public override string ToString()
        {
            return JsonSerializer.Serialize(_instance, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
