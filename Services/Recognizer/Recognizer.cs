//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Azure;
using Azure.Core;
using SpeechEnabledCoPilot.Audio;
using SpeechEnabledCoPilot.Services.Analyzer;
using SpeechEnabledCoPilot.Models;

namespace SpeechEnabledCoPilot.Services.Recognizer
{
    /// <summary>
    /// Represents the speech recognizer.
    /// </summary>
    public class Recognizer : IRecognizerResponseHandler, IAudioInputStreamHandler
    {
        RecognizerSettings settings = AppSettings.RecognizerSettings();

        private readonly object balanceLock = new object();
        private bool initialized = false;

        private SpeechConfig config;
        private AudioConfig audioConfig;
        private string? authorizationToken;
        private CancellationTokenSource source;
        private SpeechRecognizer _recognizer;

        private IAudioInputStream? audioStream;
        private PushAudioInputStream? inputStream;

        // Authorization token expires every 10 minutes. Renew it every 9 minutes.
        private TimeSpan RefreshTokenDuration = TimeSpan.FromMinutes(9);

        /// <summary>
        /// Initializes a new instance of the <see cref="Recognizer"/> class.
        /// </summary>
        public Recognizer()
        {
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
        /// Initializes the recognizer.
        /// </summary>
        /// <returns></returns>
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

            audioStream = new Microphone();
            audioStream.Start(this);

            // Creates a speech recognizer using audio config as audio input.
            // using (var _recognizer = new SpeechRecognizer(config, audioConfig))
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
                _recognizer.SessionStarted += (s, e) => { handler.onRecognitionStarted(e.SessionId); };
                _recognizer.SessionStopped += (s, e) => { handler.onRecognitionComplete(e.SessionId); };
                _recognizer.SpeechStartDetected += (s, e) => { handler.onSpeechStartDetected(e.SessionId, (long)e.Offset); };
                _recognizer.SpeechEndDetected += (s, e) => { handler.onSpeechEndDetected(e.SessionId, (long)e.Offset); };
                _recognizer.Recognizing += (s, e) => { handler.onRecognitionResult(e.SessionId, (long)e.Offset, e.Result); };
                _recognizer.Recognized += (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        handler.onFinalRecognitionResult(e.SessionId, (long)e.Offset, e.Result.Best());
                        if (analyzer != null)
                        {
                            try
                            {
                                handler.onAnalysisResult(e.SessionId, analyzer.Analyze(e.Result.Text));
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
                        NoMatchDetails noMatchDetails = NoMatchDetails.FromResult(e.Result);
                        handler.onRecognitionNoMatch(e.SessionId, (long)e.Offset, noMatchDetails.Reason.ToString(), e.Result);
                    }
                };
                _recognizer.Canceled += (s, e) =>
                {
                    if (e.Reason == CancellationReason.Error)
                    {
                        handler.onRecognitionError(e.SessionId, e.ErrorCode.ToString(), e.ErrorDetails);
                    }
                    else
                    {
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

        public void onAudioData(byte[] data)
        {
            // Push audio data to the input stream.
            if (inputStream != null) {
                inputStream.Write(data, data.Length);
            }
        }

        public void onRecognitionStarted(string sessionId)
        {
            Console.WriteLine($"[{sessionId}] Recognition started");
        }

        public void onRecognitionComplete(string sessionId)
        {
            Console.WriteLine($"[{sessionId}] Recognition complete");
        }

        public void onSpeechStartDetected(string sessionId, long offset)
        {
            Console.WriteLine($"[{sessionId}] Speech start detected: offset = {offset / 16000}");
        }

        public void onSpeechEndDetected(string sessionId, long offset)
        {
            Console.WriteLine($"[{sessionId}] Speech end detected: offset = {offset / 16000}");
            DisposeAudio();
        }

        public void onRecognitionResult(string sessionId, long offset, SpeechRecognitionResult result)
        {
            Console.WriteLine($"[{sessionId}] Streaming recognition result: {result.Text}");
        }

        public void onFinalRecognitionResult(string sessionId, long offset, System.Collections.Generic.IEnumerable<DetailedSpeechRecognitionResult> results)
        {
            var topResult = results.FirstOrDefault();
            if (topResult != null)
            {
                Console.WriteLine($"[{sessionId}] Final recognition result: {topResult.Text} (confidence score = {topResult.Confidence:F3})");
            }
            else
            {
                Console.WriteLine($"[{sessionId}] No recognition results found.");
            }
        }

        public void onRecognitionNoMatch(string sessionId, long offset, string reason, SpeechRecognitionResult result)
        {
            Console.WriteLine($"[{sessionId}] Recognition no match: {reason}");
        }

        public void onRecognitionCancelled(string sessionId, long offset, CancellationDetails details)
        {
            Console.WriteLine($"[{sessionId}] Recognition cancelled: {details.Reason.ToString()}");
            DisposeAudio();
        }

        public void onRecognitionError(string sessionId, string error, string details)
        {
            Console.WriteLine($"[{sessionId}] Recognition error: {error} - {details}");
            DisposeAudio();
        }

        public void onAnalysisResult(string sessionId, JsonElement result)
        {
            JsonElement prediction = result.GetProperty("prediction");

            string? topIntent = prediction.GetProperty("topIntent").GetString();
            JsonElement[] intents = prediction.GetProperty("intents").EnumerateArray().ToArray();
            var confidenceScore = intents[0].GetProperty("confidenceScore").GetSingle();

            Console.WriteLine($"[{sessionId}] Analysis result: ");
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

        public void onAnalysisError(string sessionId, string error, string details)
        {
            Console.WriteLine($"[{sessionId}] Analysis error: {sessionId} with error: {error} and details: {details}");
        }
    }
}
