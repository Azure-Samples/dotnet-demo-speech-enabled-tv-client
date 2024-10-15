namespace SpeechEnabledCoPilot.Endpointer
{
    public interface IEndpointer {
        void Start(IEndpointerHandler handler);
        void Stop();
        void ProcessAudio(byte[] pcm);
        int GetFrameSize();
    }
}