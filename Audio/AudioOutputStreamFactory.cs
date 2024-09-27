using SpeechEnabledCoPilot.Audio;
using SpeechEnabledCoPilot.Models;

public class AudioOutputStreamFactory
{
    public static IAudioOutputStream Create(SynthesizerSettings settings) {

        // return speaker
        if (settings.DestAudioType.Equals("speaker", StringComparison.OrdinalIgnoreCase)) {
            return new Speaker(settings.SpeechSynthesisOutputFormat);
        }

        // return audio file
        return new AudioFile(settings.DestAudioPath);
    } 
}