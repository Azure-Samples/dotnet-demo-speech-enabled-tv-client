using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechEnabledTvClient .Models
{
    /// <summary>
    /// Represents the settings for the speech synthesizer.
    /// </summary>
    public class SynthesizerSettings
    {
        public class Defaults {
            public const string SubscriptionKey = "YOUR_SUBSCRIPTION_KEY";
            public const string ServiceRegion = "eastus";
            public const string VoiceName = "en-US-AvaMultilingualNeural";
            public const string SpeechSynthesisOutputFormat = "Raw48KHz16BitMonoPcm";
            public const string DestAudioType = "speaker"; // speaker or file
            public const string DestAudioPath = "./audio"; // path to either a folder containing audio or a specific audio file
        }
        public string SubscriptionKey { get; set; } = Defaults.SubscriptionKey;
        public string ServiceRegion { get; set; } = Defaults.ServiceRegion;
        public string VoiceName { get; set; } = Defaults.VoiceName;
        public string SpeechSynthesisOutputFormat { get; set; } = Defaults.SpeechSynthesisOutputFormat;
        public string DestAudioType { get; set; } = Defaults.DestAudioType;
        public string DestAudioPath { get; set; } = Defaults.DestAudioPath;
    }
}
