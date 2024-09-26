using SpeechEnabledCoPilot.Audio;
using SpeechEnabledCoPilot.Models;

public static class AudioOutputStreamFactory
{
    public static IAudioOutputStream Create(SynthesizerSettings settings) {
        if (settings.DestAudioType != "speaker") {
            return new AudioFile(settings.DestAudioPath);
        } else {
            return new Speaker(settings.SpeechSynthesisOutputFormat);
        }
    } 
}