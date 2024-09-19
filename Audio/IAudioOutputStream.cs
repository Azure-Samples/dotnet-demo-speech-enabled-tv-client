namespace SpeechEnabledCoPilot.Audio
{
    /// <summary>
    /// Represents an audio output stream that can be started and stopped.
    /// </summary>
    public interface IAudioOutputStream
    {
        /// <summary>
        /// Starts the audio output stream.
        /// </summary>
        /// <param name="handler">The audio output stream handler.</param>
        Task Start(IAudioOutputStreamHandler handler);

        /// <summary>
        /// Stops the audio output stream.
        /// </summary>
        void Stop();

        /// <summary>
        /// Writes audio data to the audio output stream.
        /// </summary>
        /// <param name="audioData">The audio data to write.</param>
        void onAudioData(byte[] audioData);
    }
}