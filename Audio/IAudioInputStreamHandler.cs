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
        void onAudioData(byte[] data);
    }
}