namespace SpeechEnabledTvClient.Audio
{

    /// <summary>
    /// Represents an audio input stream handler that can receive audio data.
    /// </summary>
    public interface IAudioInputStreamHandler
    {

        /// <summary>
        /// Called when recording has started.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this start request.</param>
        void onRecordingStarted(string sessionId);

        /// <summary>
        /// Called when recording has stopped.
        /// </summary>
        /// <param name="sessionId">The session ID associated with this stop request.</param>
        void onRecordingStopped(string sessionId);

        /// <summary>
        /// Receives audio data.
        /// </summary>
        /// <param name="data">The audio data.</param>
        /// <param name="sessionId">The session ID associated with this audio data.</param>
        void onAudioData(string sessionId, byte[] data);
    }
}