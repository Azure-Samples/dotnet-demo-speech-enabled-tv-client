namespace SpeechEnabledTvClient.Audio
{

    /// <summary>
    /// Represents an audio output stream handler.
    /// </summary>
    public interface IAudioOutputStreamHandler
    {

        /// <summary>
        /// Called when playing has started.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this start request.</param>
        /// <param name="destination">The destination of the audio output stream.</param>
        void onPlayingStarted(string sessionId, string destination);

        /// <summary>
        /// Called when playing has stopped.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this stop request.</param>
        void onPlayingStopped(string sessionId);
    }
}