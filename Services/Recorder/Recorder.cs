using SpeechEnabledTvClient .Audio;
using Microsoft.Extensions.Logging;

namespace SpeechEnabledTvClient .Services.Recognizer
{
    /// <summary>
    /// Records audio from the microphone and saves it to file.
    /// </summary>
    public class Recorder : IAudioInputStreamHandler, IAudioOutputStreamHandler
    {
        private ILogger logger;
        private IAudioOutputStream? fileOut;

        /// <summary>
        /// Initializes a new instance of the <see cref="Recorder"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public Recorder(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Called when audio data is received from microphone. Writes audio data to file.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this audio data.</param>
        /// <param name="data">The audio data.</param>
        public void onAudioData(string sessionId, byte[] data)
        {
            if (fileOut != null) {
                fileOut.onAudioData(data);
            }
        }

        /// <summary>
        /// Called when the recorder has started.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this start request.</param>
        /// <param name="destination">The destination of the audio output stream.</param>
        public void onPlayingStarted(string sessionId, string destination)
        {
            Console.WriteLine("Recording started. Audio will be streamed to " + destination);
        }

        /// <summary>
        /// Called when the recorder has stopped.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this stop request.</param>
        public void onPlayingStopped(string sessionId)
        {
            Console.WriteLine("Recording stopped");
        }

        /// <summary>
        /// Records audio from the microphone.
        /// </summary>
        public void RecordAudio()
        {
            // Initialize microphone input
            IAudioInputStream microphone = new Microphone(logger);

            // Initialize file output
            fileOut = new AudioFile(logger);
            fileOut.Start(this);

            try
            {
                // Start capturing audio from microphone
                microphone.Start(this);
                Console.WriteLine("Recording (press any key to stop)...");
                Console.ReadKey();
                Console.WriteLine("Stopping recording...");
                microphone.Stop();
                Console.WriteLine();
            }
            finally
            {
                fileOut.Stop();
            }
        }
    }
}