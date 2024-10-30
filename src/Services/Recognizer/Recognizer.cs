//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using Azure;
using Azure.Core;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using SpeechEnabledTvClient.Audio;
using SpeechEnabledTvClient.Endpointer;
using SpeechEnabledTvClient.Models;
using SpeechEnabledTvClient.Monitoring;
using SpeechEnabledTvClient.Services.Analyzer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace SpeechEnabledTvClient.Services.Recognizer
{
    /// <summary>
    /// Represents the speech recognizer.
    /// </summary>    
    public class Recognizer : IRecognizerResponseHandler, IAudioInputStreamHandler, IAudioOutputStreamHandler, IEndpointerHandler
    {
        // properties for logging and monitoring
        private readonly ILogger logger;
        private readonly SpeechEnabledTvClient.Monitoring.Monitor monitor;
        private Activity? activity;
        private long _audioDurationInMs = 0;
        
        // Settings & configs
        private RecognizerSettings settings = AppSettings.RecognizerSettings();
        private SpeechConfig config;
        private AudioConfig audioConfig;

        // Authorization token
        private string? authorizationToken;
        private CancellationTokenSource source;
        private TimeSpan RefreshTokenDuration = TimeSpan.FromMinutes(9); // Authorization token expires every 10 minutes. Renew it every 9 minutes.
        private Task? tokenRenewTask;

        // Speech recognizer instance
        private SpeechRecognizer _recognizer;
        private TimerService? listeningTimer; // Timer to force listening for sos to stop after a certain time
        private TimerService recognizerTimer; // Timer to force recognition to stop after a certain time
        TaskCompletionSource<bool> recognitionTaskCompletionSource = new TaskCompletionSource<bool>(); // Task completion source to signal recognition completion
        TaskCompletionSource<bool> sosTaskCompletionSource = new TaskCompletionSource<bool>(); // Task completion source to signal SoS detection
        IRecognizerResponseHandler? _handler;

        // Audio input/output streams
        private IAudioInputStream? audioStream;             // audio input stream (e.g. microphone)
        private PushAudioInputStream? inputStream;          // audio input stream to feed the recognizer
        private IAudioOutputStream? audioOutFileForDebug;   // audio output stream to capture audio for debug

        // Endpointer
        private EndpointerSettings endpointerSettings = AppSettings.EndpointerSettings();
        private IEndpointer endpointer;
        private CircularBuffer<byte[]> audioBuffer; // queue audio data while waiting for StartOfSpeech
        private bool sosDetected { get; set; } // StartOfSpeech detected flag


        // Synchronize access to EndOfSpeech (EOS) detection flags
        private readonly object eosLock = new object();
        private DateTime _eosTime = DateTime.Now; // EndOfSpeech timestamp
        private DateTime eosTime {
            get {
                lock (eosLock) {
                    return _eosTime;
                }
            }
            set {
                lock (eosLock) {
                    _eosTime = value;
                }
            }
        }

        private bool _eosDetected; // EndOfSpeech detected flag
        private bool eosDetected {
            get {
                lock (eosLock) {
                    return _eosDetected;
                }
            }
            set {
                lock (eosLock) {
                    _eosDetected = value;
                }
            }
        }

        
        // Flags
        private int requestId = 0; // request ID to track the recognition requests
        private bool isSubscribedToEvents = false; // flag to check if recognizer has already subscribed to events

        /// <summary>
        /// Initializes a new instance of the <see cref="Recognizer"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="monitor">The monitor to use.</param>
        public Recognizer(ILogger logger, SpeechEnabledTvClient.Monitoring.Monitor monitor)
        {
            // Initialize the logger and monitor
            this.logger = logger;
            this.monitor = monitor.Initialize("STT");

            // Initialize the authorization token
            InitializeAuthToken().Wait();

            // Configure the audio input stream with raw 16khz PCM format.
            byte channels = 1;
            byte bitsPerSample = 16;
            uint samplesPerSecond = 16000; // or 8000
            AudioStreamFormat audioFormat = AudioStreamFormat.GetWaveFormatPCM(samplesPerSecond, bitsPerSample, channels);
            inputStream = new PushAudioInputStream(audioFormat);
            audioConfig = AudioConfig.FromStreamInput(inputStream);

            // Creates an instance of a speech config with acquired authorization token and service region (e.g., "westus").
            config = SpeechConfig.FromAuthorizationToken(authorizationToken, settings.ServiceRegion);
            config.SpeechRecognitionLanguage = settings.Language;
            config.OutputFormat = OutputFormat.Simple;
            config.RequestWordLevelTimestamps();
            config.EnableAudioLogging();
            config.SetProfanity(ProfanityOption.Masked);
            config.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, settings.InitialSilenceTimeoutMs.ToString());
            config.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, settings.EndSilenceTimeoutMs.ToString());
            config.SetProperty(PropertyId.SpeechServiceResponse_RequestSnr, true.ToString());
            config.SetProperty(PropertyId.SpeechServiceResponse_StablePartialResultThreshold, settings.StablePartialResultThreshold.ToString());
            config.SetProperty(PropertyId.SpeechServiceResponse_RecognitionLatencyMs, false.ToString());
            config.SetProperty(PropertyId.SpeechServiceResponse_JsonResult, true.ToString());

            // Define the cancellation token in order to stop the periodic renewal 
            // of authorization token after completing recognition.
            source = new CancellationTokenSource();

            // Initialize the recognizer and endpointer.
            _recognizer = new SpeechRecognizer(config, audioConfig);
            endpointer = new OpusVADEndpointer(this.logger, endpointerSettings);
            audioBuffer = new CircularBuffer<byte[]>(endpointerSettings.StartOfSpeechWindowInMs / 20);

            // Initialize the recognition timers
            recognizerTimer = new TimerService(settings.RecognitionTimeoutMs);
            if (settings.ListeningTimeoutMs > 0) {
                listeningTimer = new TimerService(settings.ListeningTimeoutMs);
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
            if (_recognizer == null)
            {
                throw new InvalidOperationException("Recognizer not initialized");
            }
            if (handler == null)
            {
                handler = this; // this class implements the handler interface to simply log the events
            }
            _handler = handler;

            // Wait for StartOfSpeech to be detected
            InitializeAudioCapture();
            if (!await AwaitStartOfSpeech().ConfigureAwait(false)) {
                return;
            }

            using (activity = monitor.activitySource.StartActivity("Recognize"))
            {
                // Get the start time to measure the total processing time
                DateTime startTime = DateTime.Now;

                // Initialize the recognizer
                await InitializeRecognizer(analyzer, handler, grammarPhrases);

                // Start continuous recognition - requires client-side logic to determine when to stop.
                await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                // Wait until End of Speech (EOS) is detected or recognition timeout is reached.
                await recognitionTaskCompletionSource.Task.ConfigureAwait(false);
                recognizerTimer.Stop();

                // Stop continuous recognition if eos is detected or recognition timeout is reached.
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

                // Cancel cancellationToken to stop the token renewal task.
                endpointer.Stop();
                // source.Cancel();

                // Calculate the total processing time
                TimeSpan processingTime = DateTime.Now - startTime;
                activity?.SetTag("TotalDurationInMs", (long)processingTime.TotalMilliseconds);
                activity?.SetTag("AudioDurationInMs", _audioDurationInMs);
            }
        }
        
        /// <summary>
        /// Initializes the audio capture.
        /// </summary>
        private void InitializeAudioCapture() {
            // Initialize flags and variables used for capturing audio
            sosDetected = eosDetected = false;
            audioStream = new Microphone(logger);
            sosTaskCompletionSource = new TaskCompletionSource<bool>();
            audioBuffer.Clear();
            _audioDurationInMs = 0;

            // Start the audio stream
            endpointer.Start(this);
            audioStream.Start(this, monitor.SessionId); // NOTE: we have a slight issue here with the session ID. It will be empty for first recognition request.

            // Start the listening timer
            listeningTimer?.Start(monitor.SessionId, OnListeningTimerExpired);
        }

        /// <summary>
        /// Waits for the start of speech to be detected.
        /// </summary>
        /// <description>
        /// This method waits for the start of speech to be detected by the endpointer.
        /// If the start of speech is not detected within the specified timeout, the method returns false.
        /// </description>
        private async Task<bool> AwaitStartOfSpeech() {
            bool sosResult = await sosTaskCompletionSource.Task.ConfigureAwait(false);
            listeningTimer?.Stop(); // Stop the listening timer in case it's still running
            
            // If StartOfSpeech is not detected, stop the audio stream and endpointer
            if (!sosResult) {
                Console.WriteLine("Timed out waiting for start of speech.");
                StopAudioStream();
                endpointer.Stop();
            }
            return sosResult;
        }

        /// <summary>
        /// Initializes the recognizer.
        /// </summary>
        /// <param name="grammarPhrases">The grammar phrases to use.</param>
        private async Task InitializeRecognizer(Analyzer.Analyzer? analyzer, IRecognizerResponseHandler? handler, string[]? grammarPhrases = null) {
            // Increment the request ID
            requestId++;
            monitor.RequestId = requestId;
            activity?.SetTag("RequestId", requestId);

            // Initialize flags and variables used for recognition
            recognitionTaskCompletionSource = new TaskCompletionSource<bool>();

            // Run task for token renewal in the background.
            tokenRenewTask = StartTokenRenewTask(source.Token, _recognizer);

            // Capture audio to file for debug if feature is enabled
            if (settings.CaptureAudio) {
                audioOutFileForDebug = new AudioFile(logger, settings.SourceAudioPath);
                await audioOutFileForDebug.Start(this);
            }

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

            // Subscribe to events
            SubscribeToEvents(analyzer, handler);
        }

        /// <summary>
        /// Calculates the latency from the end of speech to the recognition result.
        /// </summary>
        /// <returns>The latency in milliseconds.</returns>
        /// <remarks>
        /// Although final recognition results should only be available after the end of speech is detected, 
        /// there is a synchronization issue happening even though access to flags are synchronized.
        /// This method is a workaround to calculate the latency by waiting for the end of speech flag to be detected.
        /// </remarks>
        private long calculateLatency() {
            if (eosDetected) {
                return (long)(DateTime.Now - eosTime).TotalMilliseconds;
            }
            
            DateTime now = DateTime.Now;
            while (!eosDetected) {
                Thread.Sleep(20);
            }
            return (long)(DateTime.Now - now).TotalMilliseconds;
        }

        /// <summary>
        /// Subscribes to the recognizer events.
        /// </summary>
        private void SubscribeToEvents(Analyzer.Analyzer? analyzer, IRecognizerResponseHandler? handler) {
            if (!isSubscribedToEvents) {
                // Subscribe to events.
                _recognizer.SessionStarted += (s, e) => {
                    // Start the recognition timer
                    recognizerTimer.Start(e.SessionId, OnRecognitionTimerExpired);

                    // Set the session ID for tracking
                    monitor.SessionId = e.SessionId;

                    // Log the session start event
                    activity?.AddEvent(new ActivityEvent("SessionStarted",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"SessionId", e.SessionId},
                                            {"RequestId", requestId}
                                        }));
                    activity?.SetTag("SessionId", e.SessionId);

                    // Call the handler
                    handler?.onRecognitionStarted(e.SessionId);
                };
                _recognizer.SessionStopped += (s, e) => {
                    StopAudioStream();
                    
                    // Log the session stop event
                    activity?.AddEvent(new ActivityEvent("SessionStopped",
                                        DateTimeOffset.UtcNow, 
                                        new ActivityTagsCollection
                                        {
                                            {"SessionId", e.SessionId},
                                            {"RequestId", requestId}
                                        }));

                    // Clear the session ID
                    // monitor.SessionId = string.Empty;

                    // Call the handler
                    handler?.onRecognitionComplete(e.SessionId);
                };
                _recognizer.SpeechStartDetected += (s, e) => {
                    // Log the speech start detected event
                    activity?.AddEvent(new ActivityEvent("SpeechStartDetected", 
                                        DateTimeOffset.UtcNow, 
                                        new ActivityTagsCollection
                                        {
                                            {"Offset", (long)e.Offset / 10000},
                                            {"SessionId", monitor.SessionId},
                                            {"RequestId", requestId}
                                        }));
                    
                    // Call the handler
                    handler?.onSpeechStartDetected(e.SessionId, (long)e.Offset);
                };
                _recognizer.SpeechEndDetected += (s, e) => {
                    // Log the speech end detected event
                    activity?.AddEvent(new ActivityEvent("SpeechEndDetected", 
                                        DateTimeOffset.UtcNow, 
                                        new ActivityTagsCollection
                                        {
                                            {"Offset", (long)e.Offset / 10000},
                                            {"SessionId", monitor.SessionId},
                                            {"RequestId", requestId}
                                        }));

                    // Capture the end of speech time for latency calculation
                    if (!eosDetected) {
                        eosDetected = true;

                        // Signal to stop recognition
                        UpdateRecognitionTaskCompletionSource();
                    }

                    // Call the handler
                    handler?.onSpeechEndDetected(e.SessionId, (long)e.Offset);
                };
                _recognizer.Recognizing += (s, e) => {
                    // Log the recognizing event
                    activity?.AddEvent(new ActivityEvent("Recognizing", 
                                        DateTimeOffset.UtcNow, 
                                        new ActivityTagsCollection
                                        {
                                            {"Transcription", e.Result.Text},
                                            {"SessionId", monitor.SessionId},
                                            {"RequestId", requestId}
                                        }));
                    
                    // Call the handler
                    handler?.onRecognitionResult(e.SessionId, (long)e.Offset, e.Result);
                };
                _recognizer.Recognized += (s, e) =>
                {
                    // Calculate the latency from the end of speech to the recognition result
                    long latency = calculateLatency();
                    activity?.SetTag("Latency", latency);

                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        // Monitor the STT request
                        monitor.IncrementRequests("Success");
                        monitor.RecordLatency(latency, "Success");

                        // Log the recognized event
                        activity?.AddEvent(new ActivityEvent("Recognized",
                                        DateTimeOffset.UtcNow,
                                        new ActivityTagsCollection
                                        {
                                            {"Transcription", e.Result.Best()?.FirstOrDefault()?.Text},
                                            {"ConfidenceScore", e.Result.Best()?.FirstOrDefault()?.Confidence},
                                            {"SessionId", monitor.SessionId},
                                            {"RequestId", requestId}
                                        }));
                        activity?.SetTag("Disposition", "Success");
                        activity?.SetTag("Transcription", e.Result.Best()?.FirstOrDefault()?.Text);
                        activity?.SetTag("ConfidenceScore", e.Result.Best()?.FirstOrDefault()?.Confidence);

                        // Call the handler
                        handler?.onFinalRecognitionResult(e.SessionId, (long)e.Offset, e.Result.Best());
                        if (analyzer != null)
                        {
                            try
                            {
                                handler?.onAnalysisResult(e.SessionId, analyzer.Analyze(e.Result.Text, e.SessionId, requestId));
                            }
                            catch (RequestFailedException rfe)
                            {
                                handler?.onAnalysisError(e.SessionId, $"{rfe.Status}: {rfe.ErrorCode} {rfe.Message}", $"Error analyzing text: {rfe.StackTrace}");
                            }
                            catch (Exception ex)
                            {
                                handler?.onAnalysisError(e.SessionId, $"{ex.GetType().ToString()}: {ex.Message}", $"Error analyzing text: {ex.StackTrace}");
                            }
                        }
                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        // Monitor the STT request
                        monitor.IncrementRequests("NoMatch");
                        monitor.RecordLatency(latency, "NoMatch");

                        // Log the no match event
                        NoMatchDetails noMatchDetails = NoMatchDetails.FromResult(e.Result);
                        activity?.AddEvent(new ActivityEvent("NoMatch", 
                                        DateTimeOffset.UtcNow, 
                                        new ActivityTagsCollection
                                        {
                                            {"Reason", noMatchDetails.Reason.ToString()},
                                            {"SessionId", monitor.SessionId},
                                            {"RequestId", requestId}
                                        }));
                        activity?.SetTag("Disposition", "NoMatch");
                        activity?.SetTag("Reason", noMatchDetails.Reason.ToString());

                        // Call the handler
                        handler?.onRecognitionNoMatch(e.SessionId, (long)e.Offset, noMatchDetails.Reason.ToString(), e.Result);
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
                                            {"SessionId", monitor.SessionId},
                                            {"RequestId", requestId}
                                        }));
                    if (e.Reason == CancellationReason.Error)
                    {
                        // Monitor the STT request
                        monitor.IncrementRequests("Error");
                        activity?.SetTag("Disposition", "Error");
                        activity?.SetTag("Reason", e.ErrorCode.ToString());
                        activity?.SetTag("Details", e.ErrorDetails);

                        // Call the handler
                        handler?.onRecognitionError(e.SessionId, e.ErrorCode.ToString(), e.ErrorDetails);
                    }
                    else
                    {
                        // Monitor the STT request
                        monitor.IncrementRequests("Canceled");
                        activity?.SetTag("Disposition", "Error");
                        activity?.SetTag("Reason", CancellationDetails.FromResult(e.Result).Reason.ToString());

                        // Call the handler
                        handler?.onRecognitionCancelled(e.SessionId, (long)e.Offset, CancellationDetails.FromResult(e.Result));
                    }
                };

                isSubscribedToEvents = true;
            }
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

        /// <summary>
        /// Stops the audio stream.
        /// </summary>
        private void StopAudioStream()
        {
            try
            {
                if (audioStream != null) {
                    audioStream.Stop();
                    audioStream = null;
                }
            }
            catch (System.Exception)
            {
                // Ignore any exceptions that occur when stopping the audio stream.
            }
        }

        /// <summary>
        /// Streams audio data to the recognizer.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this audio data.</param>
        /// <param name="data">The audio data to stream.</param>
        private void StreamAudio(string sessionId, byte[] data) {
            // Log the audio data event
            activity?.AddEvent(new ActivityEvent("AudioData", 
                DateTimeOffset.UtcNow, 
                new ActivityTagsCollection
                {
                    {"Size", data.Length},
                    {"Duration", data.Length / 640 * 20},
                    {"SessionId", sessionId},
                    {"RequestId", requestId}
                }));

            // track how much audio is being captured
            _audioDurationInMs += (data.Length / 640 * 20);

            inputStream?.Write(data, data.Length);

            // save audio to file for debug if feature is enabled
            if (settings.CaptureAudio && audioOutFileForDebug != null) {
                audioOutFileForDebug.onAudioData(data);
            }
        }

        /// <summary>
        /// Streams buffered audio data to the recognizer.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this audio data.</param>
        /// <returns></returns>
        /// <remarks>
        /// This method streams the buffered audio data to the recognizer after StartOfSpeech is detected.
        /// </remarks>
        private void StreamBufferedAudio(string sessionId) {
            while (!audioBuffer.IsEmpty)
            {
                byte[] audioData = audioBuffer.Back();
                audioBuffer.PopBack();
                StreamAudio(sessionId, audioData);
            }
        }

        /// <summary>
        /// Updates the recognition task completion source, signaling that recognition is ready for completion.
        /// </summary>
        private void UpdateRecognitionTaskCompletionSource() {
            eosTime = DateTime.Now;
            recognitionTaskCompletionSource.TrySetResult(true);
        }

        /// Timer event handler methods

        /// <summary>
        /// Handles the recognition timer expiration event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        public void OnRecognitionTimerExpired(object? sender, TimerServiceEventArgs e)
        {
            // Log the expiration event
            activity?.AddEvent(new ActivityEvent("RecognitionTimerExpired", 
                                DateTimeOffset.UtcNow, 
                                new ActivityTagsCollection
                                {
                                    {"Timeout", (long)settings.RecognitionTimeoutMs},
                                    {"SessionId", e.SessionId},
                                    {"RequestId", requestId}
                                }));

            UpdateRecognitionTaskCompletionSource(); // Signal recognition completion
            _handler?.onRecognitionTimerExpired(e.SessionId, e.SignalTime);
        }

        /// <summary>
        /// Handles the listening timer expiration event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        public void OnListeningTimerExpired(object? sender, TimerServiceEventArgs e)
        {
            // Signal that StartOfSpeech is not detected
            sosTaskCompletionSource.TrySetResult(false);            
        }

        /// Audio stream output handler methods

        /// <summary>
        /// Handles the audio output stream started playing event
        /// </summary>
        /// <param name="sessionId">The session ID associated with this event.</param>
        /// <param name="destination">The destination of the audio output stream.</param>
        public void onPlayingStarted(string sessionId, string destination)
        {
            // Let's just capture this event locally since it's only interesting to the user
            Console.WriteLine($"Capturing audio to file: {destination}.");
        }

        /// <summary>
        /// Handles the audio output stream stopped playing event
        /// </summary>
        /// <param name="sessionId">The session ID associated with this event.</param>
        public void onPlayingStopped(string sessionId)
        {
            // Nothing to do...
        }

        /// Audio stream input handler methods

        /// <summary>
        /// Called when recording has started.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this start request.</param>
        public void onRecordingStarted(string sessionId) {
            // NOTE: we have a slight issue here with the session ID. It will be empty for first recognition request.
            _handler?.onListeningStarted(sessionId);
        }

        /// <summary>
        /// Called when recording has stopped.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this stop request.</param>
        public void onRecordingStopped(string sessionId) {
            // NOTE: we have a slight issue here with the session ID. It will be empty for first recognition request.
            _handler?.onListeningStopped(sessionId);
        }

        /// <summary>
        /// Called when audio data is received from the audio input stream.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this audio data.</param>
        /// <param name="data">The audio data.</param>
        public void onAudioData(string sessionId, byte[] data)
        {
            // Run audio through endpointer
            endpointer.ProcessAudio(data);

            // If both StartOfSpeech and EndOfSpeech are detected, we're done
            if (sosDetected && eosDetected) {
                return;
            }
            
            if (!sosDetected) {
                // Buffer audio data while waiting for StartOfSpeech
                audioBuffer.PushFront(data);

            } else if (sosDetected && !audioBuffer.IsEmpty) {
                // If we have StartOfSpeech and audio buffer, we can start streaming
                StreamBufferedAudio(sessionId);

                // And stream the current audio data
                StreamAudio(sessionId, data);
            } else {
                // Stream live audio data
                StreamAudio(sessionId, data);
            }
        }
        
        /// Endpointer handler methods

        /// <summary>
        /// Called when the start of speech is detected by the endpointer.
        /// </summary>
        /// <param name="position">The position in the audio stream.</param>
        public void OnStartOfSpeech(int position) {
            // Log the client-side speech start detected event
            activity?.AddEvent(new ActivityEvent("ClientSideOnStartOfSpeechDetected", 
                                DateTimeOffset.UtcNow, 
                                new ActivityTagsCollection
                                {
                                    {"Position", (long)position},
                                    {"SessionId", monitor.SessionId},
                                    {"RequestId", requestId}
                                }));

            sosTaskCompletionSource.TrySetResult(true);
            sosDetected = true;
            _handler?.onClientSideSpeechStartDetected(monitor.SessionId, (long)position);
        }

        /// <summary>
        /// Called when the end of speech is detected by the endpointer.
        /// </summary>
        /// <param name="position">The position in the audio stream.</param>
        public void OnEndOfSpeech(int position) {
            // Log the client-side speech end detected event
            activity?.AddEvent(new ActivityEvent("ClientSideOnEndOfSpeechDetected", 
                                DateTimeOffset.UtcNow, 
                                new ActivityTagsCollection
                                {
                                    {"Position", (long)position},
                                    {"SessionId", monitor.SessionId},
                                    {"RequestId", requestId}
                                }));

            eosDetected = true;

            // Signal that recognition is ready for completion
            UpdateRecognitionTaskCompletionSource();
            _handler?.onClientSideSpeechEndDetected(monitor.SessionId, (long)position);
        }

        /// <summary>
        /// Called when an error occurs during processing within the endpointer.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        public void OnProcessingError(int errorCode) {
            logger.LogError($"[{Identifier(monitor.SessionId)}] Endpointer failed to process audio with error code: {errorCode}");

            // Log the client-side endpointer processing error event
            activity?.AddEvent(new ActivityEvent("ClientSideEndpointerProcessingError", 
                                DateTimeOffset.UtcNow, 
                                new ActivityTagsCollection
                                {
                                    {"ErrorCode", (long)errorCode},
                                    {"SessionId", monitor.SessionId},
                                    {"RequestId", requestId}
                                }));
        }

        /// Recognizer response handler methods
        private string Identifier(string sessionId) {
            return $"{sessionId}.{requestId}";
        }

        public void onRecognitionStarted(string sessionId)
        {
            logger.LogInformation($"[{Identifier(sessionId)}] Recognition started");
        }

        public void onRecognitionComplete(string sessionId)
        {
            logger.LogInformation($"[{Identifier(sessionId)}] Recognition complete");
        }

        public void onSpeechStartDetected(string sessionId, long offset)
        {
            logger.LogInformation($"[{Identifier(sessionId)}] Speech start detected: offset = {offset / 10000}");
        }

        public void onSpeechEndDetected(string sessionId, long offset)
        {
            logger.LogInformation($"[{Identifier(sessionId)}] Speech end detected: offset = {offset / 10000}");
        }

        public void onRecognitionResult(string sessionId, long offset, SpeechRecognitionResult result)
        {
            logger.LogInformation($"[{Identifier(sessionId)}] Streaming recognition result: {result.Text}");
        }

        public void onFinalRecognitionResult(string sessionId, long offset, System.Collections.Generic.IEnumerable<DetailedSpeechRecognitionResult> results)
        {
            // Extract the top recognition result
            var topResult = results.FirstOrDefault();
            if (topResult != null)
            {
                logger.LogInformation($"[{Identifier(sessionId)}] Final recognition result: {topResult.Text} (confidence score = {topResult.Confidence:F3})");
            }
            else
            {
                logger.LogError($"[{Identifier(sessionId)}] No recognition results found.");
            }
        }

        public void onRecognitionNoMatch(string sessionId, long offset, string reason, SpeechRecognitionResult result)
        {
            logger.LogInformation($"[{Identifier(sessionId)}] Recognition no match: {reason}");
        }

        public void onRecognitionCancelled(string sessionId, long offset, CancellationDetails details)
        {
            logger.LogError($"[{Identifier(sessionId)}] Recognition cancelled: {details.Reason.ToString()}");
        }

        public void onRecognitionError(string sessionId, string error, string details)
        {
            logger.LogError($"[{Identifier(sessionId)}] Recognition error: {error} - {details}");
        }

        public void onListeningStarted(string sessionId) {
            // Let's just capture this event locally since it's only interesting to the user
            Console.WriteLine("Listening...");
        }

        public void onListeningStopped(string sessionId) {
            // Let's just capture this event locally since it's only interesting to the user
            Console.WriteLine("Listening stopped");
        }

        public void onClientSideSpeechStartDetected(string sessionId, long offset) {
            logger.LogInformation($"[{Identifier(sessionId)}] Client start of speech detected at {offset}ms");
        }

        public void onClientSideSpeechEndDetected(string sessionId, long offset) {
            logger.LogInformation($"[{Identifier(sessionId)}] Client end of speech detected at {offset}ms");
        }

        public void onRecognitionTimerExpired(string sessionId, DateTime signalTime) {
            logger.LogWarning($"[{sessionId}] Recognition timer elapsed: {signalTime:G}");
        }

        public void onAnalysisResult(string sessionId, AnalyzerResponse response)
        {
            if (!response.IsError) {
                Interpretation interpretation = response.interpretation;
                Prediction prediction = interpretation.result.prediction;
                logger.LogInformation($"[{Identifier(sessionId)}] Analysis result: ");
                logger.LogInformation($"[{Identifier(sessionId)}]\tIntent: {prediction.topIntent} ({prediction.intents[0].confidenceScore})");

                logger.LogInformation($"[{Identifier(sessionId)}]\tEntities:");
                foreach (Entity entity in prediction.entities)
                {
                    logger.LogInformation($"[{Identifier(sessionId)}]\t\tCategory: {entity.category}");
                    logger.LogInformation($"[{Identifier(sessionId)}]\t\t\tText: {entity.text}");
                    logger.LogInformation($"[{Identifier(sessionId)}]\t\t\tOffset: {entity.offset}");
                    logger.LogInformation($"[{Identifier(sessionId)}]\t\t\tLength: {entity.length}");
                    logger.LogInformation($"[{Identifier(sessionId)}]\t\t\tConfidence: {entity.confidenceScore}");

                    if (entity.nuance_CALENDARX != null)
                    {
                        logger.LogInformation($"[{Identifier(sessionId)}]\t\t\tCalendarX: {JsonSerializer.Serialize(entity.nuance_CALENDARX)}");
                    }
                }

            }
            else if (response.HasErrorResponse) {
                ErrorResponse error = response.error;
                logger.LogError($"[{Identifier(sessionId)}] Error: {error?.content?.error?.message}");
            }
            else {
                logger.LogError($"[{Identifier(sessionId)}] Error: could not parse response");
            }
        }

        public void onAnalysisError(string sessionId, string error, string details)
        {
            logger.LogError($"[{Identifier(sessionId)}] Analysis error: {Identifier(sessionId)} with error: {error} and details: {details}");
        }
    }
}
