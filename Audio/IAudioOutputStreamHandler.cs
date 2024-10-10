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
        /// <param name="sessionId">The session ID associated with this start request.</param>
        void onPlayingStarted(string destination, string sessionId = "");

        /// <summary>
        /// Called when playing has stopped.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this stop request.</param>
        void onPlayingStopped(string sessionId = "");
    }
}