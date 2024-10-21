using Microsoft.Extensions.Logging;
using SpeechEnabledTvClient.Audio;
using SpeechEnabledTvClient.Models;

public class AudioOutputStreamFactory
{
    /// <summary>
    /// Creates an audio output stream based on the settings.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="settings">The settings to use.</param>
    public static IAudioOutputStream Create(ILogger logger, SynthesizerSettings settings) {

        // return speaker
        if (settings.DestAudioType.Equals("speaker", StringComparison.OrdinalIgnoreCase)) {
            return new Speaker(logger, settings.SpeechSynthesisOutputFormat);
        }

        // return audio file
        return new AudioFile(logger, settings.DestAudioPath);
    } 
}