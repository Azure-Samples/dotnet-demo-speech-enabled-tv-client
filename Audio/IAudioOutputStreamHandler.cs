namespace SpeechEnabledCoPilot.Audio
{

    /// <summary>
    /// Represents an audio output stream handler.
    /// </summary>
    public interface IAudioOutputStreamHandler
    {

        /// <summary>
        /// Called when playing has started.
        /// </summary>
        /// <param name="destination">The destination of the audio output stream.</param>
        void onPlayingStarted(string destination);

        /// <summary>
        /// Called when playing has stopped.
        /// </summary>
        void onPlayingStopped();
    }
}