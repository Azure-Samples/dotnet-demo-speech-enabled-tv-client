namespace SpeechEnabledCoPilot.Endpointer
{
    public interface IEndpointerHandler {
        void OnStartOfSpeech(int pos);
        void OnEndOfSpeech(int pos);
    }
}