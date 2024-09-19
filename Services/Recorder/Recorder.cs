using SpeechEnabledCoPilot.Audio;

namespace SpeechEnabledCoPilot.Services.Recognizer
{
    /// <summary>
    /// Records audio from the microphone and saves it to file.
    /// </summary>
    public class Recorder : IAudioInputStreamHandler, IAudioOutputStreamHandler
    {
        private IAudioOutputStream? fileOut;

        /// <summary>
        /// Called when audio data is received from microphone. Writes audio data to file.
        /// </summary>
        /// <param name="data"></param>
        public void onAudioData(byte[] data)
        {
            if (fileOut != null) {
                fileOut.onAudioData(data);
            }
        }

        /// <summary>
        /// Called when the recorder has started.
        /// </summary>
        /// <param name="destination"></param>
        public void onPlayingStarted(string destination)
        {
            Console.WriteLine("Recording started. Audio will be saved to " + destination);
        }

        /// <summary>
        /// Called when the recorder has stopped.
        /// </summary>
        public void onPlayingStopped()
        {
            Console.WriteLine("Recording stopped");
        }

        /// <summary>
        /// Records audio from the microphone.
        /// </summary>
        public void RecordAudio()
        {
            // Initialize microphone input
            IAudioInputStream microphone = new Microphone();

            // Initialize file output
            fileOut = new AudioFile();
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