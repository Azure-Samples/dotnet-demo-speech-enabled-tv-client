using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using SpeechEnabledTvClient.Models;
using SpeechEnabledTvClient.Audio;
using SpeechEnabledTvClient.Monitoring;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SpeechEnabledTvClient.Services.Synthesizer
{

    /// <summary>
    /// Synthesizes speech from text using Azure TTS.
    /// </summary>
    class Synthesizer : IAudioOutputStreamHandler
    {
        // properties for logging and monitoring
        ILogger logger;
        SpeechEnabledTvClient.Monitoring.Monitor monitor;
        Activity? activity;

        // Settings and config
        SynthesizerSettings settings = AppSettings.SynthesizerSettings();
        private SpeechConfig? config;

        private SpeechSynthesizer speechSynthesizer;
        
        // Authorization token
        private string? authorizationToken;
        private CancellationTokenSource source;
        private TimeSpan RefreshTokenDuration = TimeSpan.FromMinutes(9); // Authorization token expires every 10 minutes. Renew it every 9 minutes.
        private Task? tokenRenewTask;
        
        // Audio stream
        private IAudioOutputStream? audioStream;

        // Flags
        private int requestId = 0; // request ID to track the recognition requests
        private bool isSubscribedToEvents = false; // flag to check if recognizer has already subscribed to events
        private DateTime startTime = DateTime.Now;
        private bool firstAudioReceived = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Synthesizer"/> class.
        /// </summary>
        public Synthesizer(ILogger logger, SpeechEnabledTvClient.Monitoring.Monitor monitor)
        {
            this.logger = logger;
            this.monitor = monitor.Initialize("TTS");

            InitializeAuthToken().Wait();

            // Creates an instance of a speech config with 
            // acquired authorization token and service region (e.g., "westus").
            config = SpeechConfig.FromAuthorizationToken(authorizationToken, settings.ServiceRegion);
            config.SpeechSynthesisVoiceName = settings.VoiceName;
            try
            {
                config.SetSpeechSynthesisOutputFormat(Enum.Parse<SpeechSynthesisOutputFormat>(settings.SpeechSynthesisOutputFormat));
            }
            catch (Exception)
            {
                logger.LogError("Invalid output format: " + settings.SpeechSynthesisOutputFormat);
                logger.LogWarning("Defaulting to Raw22050Hz16BitMonoPcm");
                config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm);
            }
            logger.LogInformation("Generating audio with output format: " + settings.SpeechSynthesisOutputFormat);

            // Define the cancellation token in order to stop the periodic renewal 
            // of authorization token after completing recognition.
            source = new CancellationTokenSource();

            speechSynthesizer = new SpeechSynthesizer(config, null); 
            // initialized = true;
        }

        /// <summary>
        /// Initializes the synthesizer.
        /// </summary>
        /// <returns></returns>
        private async Task InitializeAuthToken()
        {
            // Gets a fresh authorization token from 
            // specified subscription key and service region (e.g., "westus").
            authorizationToken = await GetToken(settings.SubscriptionKey, settings.ServiceRegion);
        }

        /// <summary>
        /// Processes the result of speech synthesis.
        /// </summary>
        /// <param name="speechSynthesisResult"></param>
        /// <param name="text"></param>
        protected void OutputSpeechSynthesisResult(SpeechSynthesisResult speechSynthesisResult, string text)
        {
            switch (speechSynthesisResult.Reason)
            {
                case ResultReason.SynthesizingAudioCompleted:
                    monitor.IncrementRequests("Success");
                    logger.LogInformation($"[{speechSynthesisResult.ResultId}.{requestId}] Speech synthesized for text: [{text}]");
                    break;
                case ResultReason.Canceled:                    
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                    logger.LogWarning($"[{speechSynthesisResult.ResultId}.{requestId}] CANCELED: Reason={cancellation.Reason}");

                    activity?.AddEvent(new ActivityEvent("Canceled",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"Reason", cancellation.Reason},
                                            {"SessionId", monitor.SessionId}
                                        }));

                    activity?.SetTag("Disposition", "Error");
                    activity?.SetTag("Reason", cancellation.Reason);

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        monitor.IncrementRequests("Error");
                        logger.LogError($"[{speechSynthesisResult.ResultId}.{requestId}] CANCELED: ErrorCode={cancellation.ErrorCode}");
                        logger.LogError($"[{speechSynthesisResult.ResultId}.{requestId}] CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                        logger.LogError($"[{speechSynthesisResult.ResultId}.{requestId}] CANCELED: Did you set the speech resource key and region values?");

                        activity?.SetTag("ErrorCode", cancellation.ErrorCode);
                        activity?.SetTag("Details", cancellation.ErrorDetails);
                    } else {
                        monitor.IncrementRequests("Canceled");
                    }
                    break;
                default:
                    break;
            }

            if (audioStream != null) {
                audioStream.Stop();
            }
        }

        /// <summary>
        /// Synthesizes speech from text.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task Synthesize(string input)
        {
            if (speechSynthesizer == null)
            {
                throw new InvalidOperationException("Synthesizer not initialized");
            }

            using (activity = monitor.activitySource.StartActivity("Synthesize"))
            {
                // Get the start time to measure the total processing time
                requestId++;
                monitor.RequestId = requestId;
                
                activity?.SetTag("RequestId", requestId);
                activity?.SetTag("Input", input);
                activity?.SetTag("VoiceName", settings.VoiceName);
                activity?.SetTag("OutputFormat", settings.SpeechSynthesisOutputFormat);

                // Initialize a few things
                startTime = DateTime.Now;
                firstAudioReceived = false;
                audioStream = AudioOutputStreamFactory.Create(logger, settings);          

                tokenRenewTask = StartTokenRenewTask(source.Token, speechSynthesizer);

                SubscribeToEvents();

                // Call the Azure TTS service and process the result.
                var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(input);
                activity?.SetTag("TotalDurationInMs", (long)(DateTime.Now - startTime).TotalMilliseconds);

                OutputSpeechSynthesisResult(speechSynthesisResult, input);
            }
        }

        private void SubscribeToEvents() {
            if (!isSubscribedToEvents) {
                /* Uncomment to enable bookmark events
                speechSynthesizer.BookmarkReached += (s, e) =>
                {
                    activity?.AddEvent(new ActivityEvent("BookmarkReached",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"SessionId", e.Result.ResultId},
                                            {"RequestID", requestId},
                                            {"AudioOffsetInMs", (e.AudioOffset + 5000) / 10000},
                                            {"Text", e.Text}
                                        }));
                };
                */

                speechSynthesizer.SynthesisCanceled += (s, e) =>
                {
                    TimeSpan latency = DateTime.Now - startTime;
                    monitor.RecordLatency((long)latency.TotalMilliseconds, "Canceled");

                    logger.LogInformation($"[{e.Result.ResultId}.{requestId}] SynthesisCanceled event");
                };

                speechSynthesizer.SynthesisCompleted += (s, e) =>
                {
                    logger.LogInformation($"[{e.Result.ResultId}.{requestId}] SynthesisCompleted event");
                    activity?.AddEvent(new ActivityEvent("SynthesisCompleted",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"SessionId", e.Result.ResultId},
                                            {"RequestID", requestId},
                                            {"AudioDataSize", e.Result.AudioData.Length},
                                            {"AudioDuration", e.Result.AudioDuration}
                                        }));
                };

                speechSynthesizer.SynthesisStarted += (s, e) =>
                {
                    // Start audio stream processing
                    audioStream?.Start(this);

                    logger.LogInformation($"[{e.Result.ResultId}.{requestId}] SynthesisStarted event");
                    
                    // Set the session ID for tracking
                    monitor.SessionId = e.Result.ResultId;

                    // Log the session start event
                    activity?.AddEvent(new ActivityEvent("SessionStarted",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"SessionId", e.Result.ResultId},
                                            {"RequestID", requestId}
                                        }));
                    activity?.SetTag("SessionId", e.Result.ResultId);
                };

                speechSynthesizer.Synthesizing += (s, e) =>
                {
                    if (!firstAudioReceived)
                    {
                        firstAudioReceived = true;
                        TimeSpan latency = DateTime.Now - startTime;
                        monitor.RecordLatency((long)latency.TotalMilliseconds, "Success");
                    }
                    logger.LogInformation($"[{e.Result.ResultId}.{requestId}] Synthesizing event");
                    activity?.AddEvent(new ActivityEvent("Synthesizing",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"SessionId", e.Result.ResultId},
                                            {"RequestID", requestId},
                                            {"AudioDataLength", e.Result.AudioData.Length}
                                        }));
                    // Send audio data to audio stream
                    audioStream?.onAudioData(e.Result.AudioData);
                };

                /* Uncomment to enable viseme events
                speechSynthesizer.VisemeReceived += (s, e) =>
                {
                    activity?.AddEvent(new ActivityEvent("VisemeReceived",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"SessionId", e.Result.ResultId},
                                            {"RequestID", requestId},
                                            {"AudioOffsetInMs", (e.AudioOffset + 5000) / 10000},
                                            {"VisemeId", e.VisemeId}
                                        }));
                };
                */

                /* Uncomment to enable word boundary events
                speechSynthesizer.WordBoundary += (s, e) =>
                {
                    activity?.AddEvent(new ActivityEvent("WordBoundary",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"SessionId", e.Result.ResultId},
                                            {"RequestID", requestId},
                                            {"AudioOffsetInMs", (e.AudioOffset + 5000) / 10000},
                                            {"BoundaryType", e.BoundaryType},
                                            {"Duration", e.Duration},
                                            {"Text", e.Text},
                                            {"TextOffset", e.TextOffset},
                                            {"WordLength", e.WordLength}
                                        }));
                };
                */
                isSubscribedToEvents = true;
            }
        }

        // Renews authorization token periodically until cancellationToken is cancelled.
        protected Task StartTokenRenewTask(CancellationToken cancellationToken, SpeechSynthesizer synthesizer)
        {
            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(RefreshTokenDuration, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        synthesizer.AuthorizationToken = await GetToken(settings.SubscriptionKey, settings.ServiceRegion);
                    }
                }
            });
        }

        // Gets an authorization token by sending a POST request to the token service.
        protected async Task<string> GetToken(string subscriptionKey, string region)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                UriBuilder uriBuilder = new UriBuilder("https://" + region + ".api.cognitive.microsoft.com/sts/v1.0/issueToken");

                using (var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null))
                {
                    // Console.WriteLine("Token Uri: {0}", uriBuilder.Uri.AbsoluteUri);
                    if (result.IsSuccessStatusCode)
                    {
                        return await result.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        throw new HttpRequestException($"Cannot get token from {uriBuilder.ToString()}. Error: {result.StatusCode}");
                    }
                }
            }
        }

        /// <summary>
        /// Called when audio output stream is started.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="fileName"></param>
        public void onPlayingStarted(string sessionId, string fileName)
        {
            logger.LogInformation($"[{monitor.SessionId}.{requestId}] Playing started. Audio is being saved to {fileName}");
        }

        /// <summary>
        /// Called when audio output stream is stopped.
        /// </summary>
        /// <param name="sessionId"></param>
        public void onPlayingStopped(string sessionId)
        {
            logger.LogInformation($"[{monitor.SessionId}.{requestId}] Playing stopped");
        }
    }
}