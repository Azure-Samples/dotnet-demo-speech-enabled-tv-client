namespace SpeechEnabledCoPilot.Endpointer
{
    /// <summary>
    /// Interface for the endpointer.
    /// </summary>
    /// <remarks>
    /// Used to define the endpointer interface.
    /// </remarks>
    public interface IEndpointer {
        /// <summary>
        /// Starts the endpointer.
        /// </summary>
        /// <param name="handler">The endpointer handler.</param>
        /// <returns></returns>
        void Start(IEndpointerHandler handler);

        /// <summary>
        /// Stops the endpointer.
        /// </summary>
        /// <returns></returns>
        void Stop();

        /// <summary>
        /// Processes the audio.
        /// </summary>
        /// <param name="pcm">The PCM audio data.</param>
        /// <returns></returns>
        void ProcessAudio(byte[] pcm);

        /// <summary>
        /// Gets the frame size.
        /// </summary>
        /// <returns>The frame size.</returns>
        int GetFrameSize();
    }
}