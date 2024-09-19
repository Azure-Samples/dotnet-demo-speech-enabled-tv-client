using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using SpeechEnabledCoPilot.Models;
using SpeechEnabledCoPilot.Audio;

namespace SpeechEnabledCoPilot.Services.Synthesizer
{

    /// <summary>
    /// Synthesizes speech from text using Azure TTS.
    /// </summary>
    class Synthesizer : IAudioOutputStreamHandler
    {
        SynthesizerSettings settings = AppSettings.SynthesizerSettings();

        private bool initialized = false;
        private SpeechConfig? config;
        private string? authorizationToken;
        private CancellationTokenSource source;

        private IAudioOutputStream? audioStream;

        // Authorization token expires every 10 minutes. Renew it every 9 minutes.
        private TimeSpan RefreshTokenDuration = TimeSpan.FromMinutes(9);

        /// <summary>
        /// Initializes a new instance of the <see cref="Synthesizer"/> class.
        /// </summary>
        public Synthesizer()
        {
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
                Console.WriteLine("Invalid output format: " + settings.SpeechSynthesisOutputFormat);
                Console.WriteLine("Defaulting to Raw22050Hz16BitMonoPcm");
                config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm);
            }
            System.Console.WriteLine("Generating audio with output format: " + settings.SpeechSynthesisOutputFormat);

            // Define the cancellation token in order to stop the periodic renewal 
            // of authorization token after completing recognition.
            source = new CancellationTokenSource();

            initialized = true;
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
                    Console.WriteLine($"Speech synthesized for text: [{text}]");
                    break;
                case ResultReason.Canceled:
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                    Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                        Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
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
            if (!initialized)
            {
                throw new InvalidOperationException("Synthesizer not initialized");
            }

            audioStream = new AudioFile(settings.DestAudioPath);

            using (var speechSynthesizer = new SpeechSynthesizer(config, null))
            {
                var tokenRenewTask = StartTokenRenewTask(source.Token, speechSynthesizer);

                // Subscribe to events

                /* Uncomment to enable bookmark events
                speechSynthesizer.BookmarkReached += (s, e) =>
                {
                    Console.WriteLine($"BookmarkReached event:" +
                        $"\r\n\tAudioOffset: {(e.AudioOffset + 5000) / 10000}ms" +
                        $"\r\n\tText: \"{e.Text}\".");
                };
                */

                speechSynthesizer.SynthesisCanceled += (s, e) =>
                {
                    Console.WriteLine("SynthesisCanceled event");
                };

                speechSynthesizer.SynthesisCompleted += (s, e) =>
                {
                    Console.WriteLine($"SynthesisCompleted event:" +
                        $"\r\n\tAudioData: {e.Result.AudioData.Length} bytes" +
                        $"\r\n\tAudioDuration: {e.Result.AudioDuration}");
                };

                speechSynthesizer.SynthesisStarted += (s, e) =>
                {
                    // Start audio stream processing
                    audioStream.Start(this);
                    Console.WriteLine("SynthesisStarted event");
                };

                speechSynthesizer.Synthesizing += (s, e) =>
                {
                    Console.WriteLine($"Synthesizing event:" +
                        $"\r\n\tAudioData: {e.Result.AudioData.Length} bytes");

                    // Send audio data to audio stream
                    audioStream.onAudioData(e.Result.AudioData);
                };

                /* Uncomment to enable viseme events
                speechSynthesizer.VisemeReceived += (s, e) =>
                {
                    Console.WriteLine($"VisemeReceived event:" +
                        $"\r\n\tAudioOffset: {(e.AudioOffset + 5000) / 10000}ms" +
                        $"\r\n\tVisemeId: {e.VisemeId}");
                };
                */

                /* Uncomment to enable word boundary events
                speechSynthesizer.WordBoundary += (s, e) =>
                {
                    Console.WriteLine($"WordBoundary event:" +
                        // Word, Punctuation, or Sentence
                        $"\r\n\tBoundaryType: {e.BoundaryType}" +
                        $"\r\n\tAudioOffset: {(e.AudioOffset + 5000) / 10000}ms" +
                        $"\r\n\tDuration: {e.Duration}" +
                        $"\r\n\tText: \"{e.Text}\"" +
                        $"\r\n\tTextOffset: {e.TextOffset}" +
                        $"\r\n\tWordLength: {e.WordLength}");
                };
                */

                // Call the Azure TTS service and process the result.
                var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(input);
                OutputSpeechSynthesisResult(speechSynthesisResult, input);
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
        /// <param name="fileName"></param>
        public void onPlayingStarted(string fileName)
        {
            Console.WriteLine($"Playing started. Audio is being saved to {fileName}");
        }

        /// <summary>
        /// Called when audio output stream is stopped.
        /// </summary>
        public void onPlayingStopped()
        {
            Console.WriteLine("Playing stopped");
        }
    }
}