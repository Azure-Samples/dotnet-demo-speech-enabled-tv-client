//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Core;
using SpeechEnabledCoPilot.Audio;
using SpeechEnabledCoPilot.Services.Analyzer;
using SpeechEnabledCoPilot.Models;
using SpeechEnabledCoPilot.Monitoring;

namespace SpeechEnabledCoPilot.Services.Recognizer
{
    /// <summary>
    /// Represents the speech recognizer.
    /// </summary>    
    public class Recognizer : IRecognizerResponseHandler, IAudioInputStreamHandler, IAudioOutputStreamHandler
    {
        private ILogger logger;
        private SpeechEnabledCoPilot.Monitoring.Monitor monitor;
        private Activity? activity;
        private long _audioDurationInMs = 0;

        private RecognizerSettings settings = AppSettings.RecognizerSettings();

        private readonly object balanceLock = new object();
        private bool initialized = false;

        private SpeechConfig config;
        private AudioConfig audioConfig;
        private string? authorizationToken;
        private CancellationTokenSource source;
        private SpeechRecognizer _recognizer;

        private IAudioInputStream? audioStream;
        private PushAudioInputStream? inputStream;
        private IAudioOutputStream? audioOutFileForDebug;

        // Authorization token expires every 10 minutes. Renew it every 9 minutes.
        private TimeSpan RefreshTokenDuration = TimeSpan.FromMinutes(9);

        /// <summary>
        /// Initializes a new instance of the <see cref="Recognizer"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="monitor">The monitor to use.</param>
        public Recognizer(ILogger logger, SpeechEnabledCoPilot.Monitoring.Monitor monitor)
        {
            this.logger = logger;
            this.monitor = monitor.Initialize("STT");

            InitializeAuthToken().Wait();
            
            // Configure the audio input stream with raw 16khz PCM format.
            byte channels = 1;
            byte bitsPerSample = 16;
            uint samplesPerSecond = 16000; // or 8000
            AudioStreamFormat audioFormat = AudioStreamFormat.GetWaveFormatPCM(samplesPerSecond, bitsPerSample, channels);
            inputStream = new PushAudioInputStream(audioFormat);
            audioConfig = AudioConfig.FromStreamInput(inputStream);

            // Creates an instance of a speech config with 
            // acquired authorization token and service region (e.g., "westus").
            // The default language is "en-us".
            config = SpeechConfig.FromAuthorizationToken(authorizationToken, settings.ServiceRegion);
            config.SpeechRecognitionLanguage = settings.Language;
            config.OutputFormat = OutputFormat.Simple;
            config.RequestWordLevelTimestamps();
            config.EnableAudioLogging();
            config.SetProfanity(ProfanityOption.Masked);
            // config.EnableDictation();
            config.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, settings.InitialSilenceTimeoutMs.ToString());
            config.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, settings.EndSilenceTimeoutMs.ToString());
            config.SetProperty(PropertyId.SpeechServiceResponse_RequestSnr, true.ToString());
            config.SetProperty(PropertyId.SpeechServiceResponse_StablePartialResultThreshold, settings.StablePartialResultThreshold.ToString());
            config.SetProperty(PropertyId.SpeechServiceResponse_RecognitionLatencyMs, false.ToString());
            config.SetProperty(PropertyId.SpeechServiceResponse_JsonResult, true.ToString());

            // Define the cancellation token in order to stop the periodic renewal 
            // of authorization token after completing recognition.
            source = new CancellationTokenSource();

            // Initialize the recognizer.
            _recognizer = new SpeechRecognizer(config, audioConfig);

            initialized = true;
        }

        /// <summary>
        /// Initializes the authorization token.
        /// </summary>
        /// <returns>The task.</returns>
        private async Task InitializeAuthToken()
        {
            // Gets a fresh authorization token from 
            // specified subscription key and service region (e.g., "westus").
            authorizationToken = await GetToken(settings.SubscriptionKey, settings.ServiceRegion);
        }

        /// <summary>
        /// Disposes the audio stream.
        /// </summary>
        private void DisposeAudio()
        {
            try
            {
                if (audioStream != null) { audioStream.Stop(); }
                if (inputStream != null) { inputStream.Close(); }
            }
            catch (System.Exception)
            {
                // Ignore any exceptions that occur when stopping the audio stream.
            }
        }

        /// <summary>
        /// Recognizes speech from the microphone.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="grammarPhrases"></param>
        /// <returns></returns>
        public async Task Recognize(IRecognizerResponseHandler? handler, string[]? grammarPhrases = null)
        {
            await Recognize(null, handler, grammarPhrases);
        }

        /// <summary>
        /// Recognizes speech from the microphone and analyzes the recognized text.
        /// </summary>
        /// <param name="analyzer"></param>
        /// <param name="handler"></param>
        /// <param name="grammarPhrases"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task Recognize(Analyzer.Analyzer? analyzer, IRecognizerResponseHandler? handler, string[]? grammarPhrases = null)
        {
            if (!initialized)
            {
                throw new InvalidOperationException("Recognizer not initialized");
            }
            if (handler == null)
            {
                handler = this;
            }

            using (activity = monitor.activitySource.StartActivity("Recognize"))
            {
                // Get the start time to measure the total processing time
                DateTime startTime = DateTime.Now;
                DateTime eosTime = DateTime.Now;

                // Capture audio to file for debug if feature is enabled
                if (settings.CaptureAudio) {
                    audioOutFileForDebug = new AudioFile(logger, settings.SourceAudioPath);
                    await audioOutFileForDebug.Start(this);
                }

                // Start the audio stream
                audioStream = new Microphone(logger);
                audioStream.Start(this);

                // Start the recognizer
                using (_recognizer)
                {
                    // Run task for token renewal in the background.
                    var tokenRenewTask = StartTokenRenewTask(source.Token, _recognizer);

                    // Initialize and set inline grammar if provided.
                    PhraseListGrammar grammarList = PhraseListGrammar.FromRecognizer(_recognizer);
                    grammarList.Clear();
                    if (grammarPhrases != null)
                    {
                        foreach (var item in grammarPhrases)
                        {
                            grammarList.AddPhrase(item);
                        }
                    }

                    // Subscribe to events.
                    _recognizer.SessionStarted += (s, e) => {
                        // Set the session ID for tracking
                        monitor.SessionId = e.SessionId;

                        // Log the session start event
                        activity?.AddEvent(new ActivityEvent("SessionStarted",
                                            DateTimeOffset.UtcNow,
                                            new ActivityTagsCollection
                                            {
                                                {"SessionId", e.SessionId}
                                            }));
                        activity?.SetTag("SessionId", e.SessionId);

                        // Call the handler
                        handler.onRecognitionStarted(e.SessionId);
                    };
                    _recognizer.SessionStopped += (s, e) => {
                        // Clear the session ID
                        monitor.SessionId = string.Empty;

                        // Log the session stop event
                        activity?.AddEvent(new ActivityEvent("SessionStopped",
                                            DateTimeOffset.UtcNow, 
                                            new ActivityTagsCollection
                                            {
                                                {"SessionId", e.SessionId}
                                            }));
                        activity?.SetTag("SessionId", e.SessionId);

                        // Call the handler
                        handler.onRecognitionComplete(e.SessionId);
                    };
                    _recognizer.SpeechStartDetected += (s, e) => {
                        // Log the speech start detected event
                        activity?.AddEvent(new ActivityEvent("SpeechStartDetected", 
                                            DateTimeOffset.UtcNow, 
                                            new ActivityTagsCollection
                                            {
                                                {"Offset", (long)e.Offset / 10000},
                                                {"SessionId", monitor.SessionId}
                                            }));
                        
                        // Call the handler
                        handler.onSpeechStartDetected(e.SessionId, (long)e.Offset);
                    };
                    _recognizer.SpeechEndDetected += (s, e) => {
                        // Capture the end of speech time for latency calculation
                        eosTime = DateTime.Now;

                        // Log the speech end detected event
                        activity?.AddEvent(new ActivityEvent("SpeechEndDetected", 
                                            DateTimeOffset.UtcNow, 
                                            new ActivityTagsCollection
                                            {
                                                {"Offset", (long)e.Offset / 10000},
                                                {"SessionId", monitor.SessionId}
                                            }));

                        // Call the handler
                        handler.onSpeechEndDetected(e.SessionId, (long)e.Offset);
                    };
                    _recognizer.Recognizing += (s, e) => {
                        // Log the recognizing event
                        activity?.AddEvent(new ActivityEvent("Recognizing", 
                                            DateTimeOffset.UtcNow, 
                                            new ActivityTagsCollection
                                            {
                                                {"Transcription", e.Result.Text},
                                                {"SessionId", monitor.SessionId}
                                            }));
                        
                        // Call the handler
                        handler.onRecognitionResult(e.SessionId, (long)e.Offset, e.Result);
                    };
                    _recognizer.Recognized += (s, e) =>
                    {
                        // Calculate the latency from the end of speech to the recognition result
                        TimeSpan latency = DateTime.Now - eosTime;

                        if (e.Result.Reason == ResultReason.RecognizedSpeech)
                        {
                            // Monitor the STT request
                            monitor.IncrementRequests("Success");
                            monitor.RecordLatency((long)latency.TotalMilliseconds, "Success");

                            // Log the recognized event
                            activity?.AddEvent(new ActivityEvent("Recognized",
                                            DateTimeOffset.UtcNow,
                                            new ActivityTagsCollection
                                            {
                                                {"Transcription", e.Result.Best()?.FirstOrDefault()?.Text},
                                                {"ConfidenceScore", e.Result.Best()?.FirstOrDefault()?.Confidence},
                                                {"SessionId", monitor.SessionId}
                                            }));
                            activity?.SetTag("Disposition", "Success");
                            activity?.SetTag("Transcription", e.Result.Best()?.FirstOrDefault()?.Text);
                            activity?.SetTag("ConfidenceScore", e.Result.Best()?.FirstOrDefault()?.Confidence);

                            // Call the handler
                            handler.onFinalRecognitionResult(e.SessionId, (long)e.Offset, e.Result.Best());
                            if (analyzer != null)
                            {
                                try
                                {
                                    handler.onAnalysisResult(e.SessionId, analyzer.Analyze(e.Result.Text, e.SessionId));
                                }
                                catch (RequestFailedException rfe)
                                {
                                    handler.onAnalysisError(e.SessionId, $"{rfe.Status}: {rfe.ErrorCode} {rfe.Message}", $"Error analyzing text: {rfe.StackTrace}");
                                }
                                catch (Exception ex)
                                {
                                    handler.onAnalysisError(e.SessionId, $"{ex.GetType().ToString()}: {ex.Message}", $"Error analyzing text: {ex.StackTrace}");
                                }
                            }
                        }
                        else if (e.Result.Reason == ResultReason.NoMatch)
                        {
                            // Monitor the STT request
                            monitor.IncrementRequests("NoMatch");
                            monitor.RecordLatency((long)latency.TotalMilliseconds, "NoMatch");

                            // Log the no match event
                            NoMatchDetails noMatchDetails = NoMatchDetails.FromResult(e.Result);
                            activity?.AddEvent(new ActivityEvent("NoMatch", 
                                            DateTimeOffset.UtcNow, 
                                            new ActivityTagsCollection
                                            {
                                                {"Reason", noMatchDetails.Reason.ToString()},
                                                {"SessionId", monitor.SessionId}
                                            }));
                            activity?.SetTag("Disposition", "NoMatch");
                            activity?.SetTag("Reason", noMatchDetails.Reason.ToString());

                            // Call the handler
                            handler.onRecognitionNoMatch(e.SessionId, (long)e.Offset, noMatchDetails.Reason.ToString(), e.Result);
                        }
                    };
                    _recognizer.Canceled += (s, e) =>
                    {
                        // Log the canceled event
                        activity?.AddEvent(new ActivityEvent("Canceled",
                                            DateTimeOffset.UtcNow,
                                            new ActivityTagsCollection
                                            {
                                                {"Reason", e.Reason.ToString()},
                                                {"SessionId", monitor.SessionId}
                                            }));
                        if (e.Reason == CancellationReason.Error)
                        {
                            // Monitor the STT request
                            monitor.IncrementRequests("Error");
                            activity?.SetTag("Disposition", "Error");
                            activity?.SetTag("Reason", e.ErrorCode.ToString());
                            activity?.SetTag("Details", e.ErrorDetails);

                            // Call the handler
                            handler.onRecognitionError(e.SessionId, e.ErrorCode.ToString(), e.ErrorDetails);
                        }
                        else
                        {
                            // Monitor the STT request
                            monitor.IncrementRequests("Canceled");
                            activity?.SetTag("Disposition", "Error");
                            activity?.SetTag("Reason", CancellationDetails.FromResult(e.Result).Reason.ToString());

                            // Call the handler
                            handler.onRecognitionCancelled(e.SessionId, (long)e.Offset, CancellationDetails.FromResult(e.Result));
                        }
                    };

                    // Start single-turn recognition with server-side endpoint detection.
                    await _recognizer.RecognizeOnceAsync().ConfigureAwait(false);

                    // Start continuous recognition - requires client-side logic to determine when to stop.
                    // await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
                    // Console.WriteLine("Press any key to stop");
                    // Console.ReadKey();
                    // await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

                    // Cancel cancellationToken to stop the token renewal task.
                    source.Cancel();

                    // Calculate the total processing time
                    TimeSpan processingTime = DateTime.Now - startTime;
                    activity?.SetTag("TotalDurationInMs", (long)processingTime.TotalMilliseconds);
                    activity?.SetTag("AudioDurationInMs", _audioDurationInMs);
                }
            }
        }

        // Renews authorization token periodically until cancellationToken is cancelled.
        protected Task StartTokenRenewTask(CancellationToken cancellationToken, SpeechRecognizer recognizer)
        {
            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(RefreshTokenDuration, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        recognizer.AuthorizationToken = await GetToken(settings.SubscriptionKey, settings.ServiceRegion);
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
        /// Handles the audio output stream started playing event
        /// </summary>
        /// <param name="sessionId">The session ID associated with this event.</param>
        /// <param name="destination">The destination of the audio output stream.</param>
        public void onPlayingStarted(string sessionId, string destination)
        {
            // Let's just capture this event locally since it's only interesting to the user
            Console.WriteLine($"[{sessionId}] Capturing audio to file: {destination}.");
        }

        /// <summary>
        /// Handles the audio output stream stopped playing event
        /// </summary>
        /// <param name="sessionId">The session ID associated with this event.</param>
        public void onPlayingStopped(string sessionId)
        {
            // Nothing to do...
        }

        /// <summary>
        /// Called when audio data is received from the audio input stream.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this audio data.</param>
        /// <param name="data">The audio data.</param>
        public void onAudioData(string sessionId, byte[] data)
        {
            // Log the audio data event
            activity?.AddEvent(new ActivityEvent("AudioData", 
                DateTimeOffset.UtcNow, 
                new ActivityTagsCollection
                {
                    {"Size", data.Length},
                    {"Duration", data.Length / 640 * 20},
                    {"SessionId", sessionId}
                }));

            // track how much audio is being captured
            _audioDurationInMs += (data.Length / 640 * 20);

            // save audio to file for debug if feature is enabled
            if (settings.CaptureAudio && audioOutFileForDebug != null) {
                audioOutFileForDebug.onAudioData(data);
            }

            // Push audio data to the input stream.
            if (inputStream != null) {
                inputStream.Write(data, data.Length);
            }
        }

        public void onRecognitionStarted(string sessionId)
        {
            logger.LogInformation($"[{sessionId}] Recognition started");
        }

        public void onRecognitionComplete(string sessionId)
        {
            logger.LogInformation($"[{sessionId}] Recognition complete");
        }

        public void onSpeechStartDetected(string sessionId, long offset)
        {
            logger.LogInformation($"[{sessionId}] Speech start detected: offset = {offset / 10000}");
        }

        public void onSpeechEndDetected(string sessionId, long offset)
        {
            logger.LogInformation($"[{sessionId}] Speech end detected: offset = {offset / 10000}");

            // Stop the audio stream
            DisposeAudio();
        }

        public void onRecognitionResult(string sessionId, long offset, SpeechRecognitionResult result)
        {
            logger.LogInformation($"[{sessionId}] Streaming recognition result: {result.Text}");
        }

        public void onFinalRecognitionResult(string sessionId, long offset, System.Collections.Generic.IEnumerable<DetailedSpeechRecognitionResult> results)
        {
            // Extract the top recognition result
            var topResult = results.FirstOrDefault();
            if (topResult != null)
            {
                logger.LogInformation($"[{sessionId}] Final recognition result: {topResult.Text} (confidence score = {topResult.Confidence:F3})");
            }
            else
            {
                logger.LogError($"[{sessionId}] No recognition results found.");
            }
        }

        public void onRecognitionNoMatch(string sessionId, long offset, string reason, SpeechRecognitionResult result)
        {
            logger.LogInformation($"[{sessionId}] Recognition no match: {reason}");
        }

        public void onRecognitionCancelled(string sessionId, long offset, CancellationDetails details)
        {
            logger.LogError($"[{sessionId}] Recognition cancelled: {details.Reason.ToString()}");

            // Stop the audio stream
            DisposeAudio();
        }

        public void onRecognitionError(string sessionId, string error, string details)
        {
            logger.LogError($"[{sessionId}] Recognition error: {error} - {details}");

            // Stop the audio stream
            DisposeAudio();
        }

        public void onAnalysisResult(string sessionId, AnalyzerResponse response)
        {
            if (!response.IsError) {
                Interpretation interpretation = response.interpretation;
                Prediction prediction = interpretation.result.prediction;
                logger.LogInformation($"[{sessionId}] Analysis result: ");
                logger.LogInformation($"[{sessionId}]\tIntent: {prediction.topIntent} ({prediction.intents[0].confidenceScore})");

                logger.LogInformation($"[{sessionId}]\tEntities:");
                foreach (Entity entity in prediction.entities)
                {
                    logger.LogInformation($"[{sessionId}]\t\tCategory: {entity.category}");
                    logger.LogInformation($"[{sessionId}]\t\t\tText: {entity.text}");
                    logger.LogInformation($"[{sessionId}]\t\t\tOffset: {entity.offset}");
                    logger.LogInformation($"[{sessionId}]\t\t\tLength: {entity.length}");
                    logger.LogInformation($"[{sessionId}]\t\t\tConfidence: {entity.confidenceScore}");
                }

            }
            else if (response.HasErrorResponse) {
                ErrorResponse error = response.error;
                logger.LogError($"[{sessionId}] Error: {error?.content?.error?.message}");
            }
            else {
                logger.LogError($"[{sessionId}] Error: could not parse response");
            }
        }

        public void onAnalysisError(string sessionId, string error, string details)
        {
            logger.LogError($"[{sessionId}] Analysis error: {sessionId} with error: {error} and details: {details}");
        }
    }
}
