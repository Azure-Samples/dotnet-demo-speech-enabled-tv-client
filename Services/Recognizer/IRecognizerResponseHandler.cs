using Microsoft.CognitiveServices.Speech;
using SpeechEnabledTvClient.Services.Analyzer;
using System.Text.Json;

namespace SpeechEnabledTvClient.Services.Recognizer
{
    /// <summary>
    /// Represents the response handler for the speech recognizer.
    /// </summary>
    public interface IRecognizerResponseHandler
    {

        /// <summary>
        /// Called when the recognition has started.
        /// </summary>
        /// <param name="sessionId"></param>
        void onRecognitionStarted(string sessionId);

        /// <summary>
        /// Called when the recognition has completed.
        /// </summary>
        /// <param name="sessionId"></param>
        void onRecognitionComplete(string sessionId);

        /// <summary>
        /// Called when start of speech has been detected.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="offset"></param>
        void onSpeechStartDetected(string sessionId, long offset);

        void onClientSideSpeechStartDetected(string sessionId, long offset);
        void onClientSideSpeechEndDetected(string sessionId, long offset);
        void onRecognitionTimerExpired(string sessionId, DateTime signalTime);

        /// <summary>
        /// Called when end of speech has been detected.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="offset"></param>
        void onSpeechEndDetected(string sessionId, long offset);

        /// <summary>
        /// Called when a recognition result is received.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="offset"></param>
        /// <param name="result"></param>
        void onRecognitionResult(string sessionId, long offset, SpeechRecognitionResult result);

        /// <summary>
        /// Called when a final recognition result with transcription is received.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="offset"></param>
        /// <param name="results"></param>
        void onFinalRecognitionResult(string sessionId, long offset, System.Collections.Generic.IEnumerable<DetailedSpeechRecognitionResult> results);

        /// <summary>
        /// Called when a final recognition result fails to return a transcription due to poor audio.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="offset"></param>
        /// <param name="reason"></param>
        /// <param name="result"></param>
        void onRecognitionNoMatch(string sessionId, long offset, string reason, SpeechRecognitionResult result);

        /// <summary>
        /// Called when the recognition is cancelled.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="offset"></param>
        /// <param name="details"></param>
        void onRecognitionCancelled(string sessionId, long offset, CancellationDetails details);

        /// <summary>
        /// Called when an error occurs during recognition.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="error"></param>
        /// <param name="details"></param>
        void onRecognitionError(string sessionId, string error, string details);

        /// <summary>
        /// Called when the recognizer has received a CLU analysis result.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="result"></param>
        void onAnalysisResult(string sessionId, AnalyzerResponse result);

        /// <summary>
        /// Called when the recognizer has received a CLU analysis error.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="error"></param>
        /// <param name="details"></param>
        void onAnalysisError(string sessionId, string error, string details);
    }
}
