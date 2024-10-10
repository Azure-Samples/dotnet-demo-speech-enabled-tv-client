namespace SpeechEnabledCoPilot.Audio
{

    /// <summary>
    /// Represents an audio input stream handler that can receive audio data.
    /// </summary>
    public interface IAudioInputStreamHandler
    {

        /// <summary>
        /// Receives audio data.
        /// </summary>
        /// <param name="data">The audio data.</param>
        /// <param name="sessionId">The session ID associated with this audio data.</param>
        void onAudioData(string sessionId, byte[] data);
    }
}