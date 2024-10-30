namespace SpeechEnabledTvClient.Endpointer
{
    /// <summary>
    /// Interface for the endpointer handler.
    /// </summary>
    public interface IEndpointerHandler {
        /// <summary>
        /// Called when the start of speech is detected.
        /// </summary>
        /// <param name="pos">The position in the audio stream.</param>
        void OnStartOfSpeech(int pos);

        /// <summary>
        /// Called when the end of speech is detected.
        /// </summary>
        /// <param name="pos">The position in the audio stream.</param>
        void OnEndOfSpeech(int pos);

        /// <summary>
        /// Called when an error occurs during processing.
        /// </summary>
        /// <param name="pos">The position in the audio stream.</param>
        void OnProcessingError(int errorCode);
    }
}