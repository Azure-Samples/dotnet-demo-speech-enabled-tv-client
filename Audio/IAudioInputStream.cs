namespace SpeechEnabledCoPilot.Audio {
    
    /// <summary>
    /// Represents an audio input stream that can be started and stopped.
    /// </summary>
    public interface IAudioInputStream {
        /// <summary>
        /// Starts the audio input stream.
        /// </summary>
        /// <param name="handler">The audio input stream handler.</param>
        void Start(IAudioInputStreamHandler handler);
        
        /// <summary>
        /// Stops the audio input stream.
        /// </summary>
        void Stop();
    }
}