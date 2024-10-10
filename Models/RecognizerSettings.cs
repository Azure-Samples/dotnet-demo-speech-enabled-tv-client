using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechEnabledCoPilot.Models
{
    /// <summary>
    /// Represents the settings for the speech recognizer.
    /// </summary>
    public class RecognizerSettings
    {
        public class Defaults
        {
            public const string SubscriptionKey = "YOUR_SUBSCRIPTION_KEY";
            public const string ServiceRegion = "eastus";
            public const string Language = "en-US";
            public const string SourceAudioType = "microphone"; // microphone or file
            public const string SourceAudioPath = "./audio"; // path to either a folder containing audio or a specific audio file
            public const string ProfanityOption = "masked"; // raw or masked
            public const int InitialSilenceTimeoutMs = 10000; // in milliseconds
            public const int EndSilenceTimeoutMs = 1200; // in milliseconds
            public const int StablePartialResultThreshold = 2;
            public const bool CaptureAudio = false;
        }

        public string SubscriptionKey { get; set; } = Defaults.SubscriptionKey;
        public string ServiceRegion { get; set; } = Defaults.ServiceRegion;
        public string Language { get; set; } = Defaults.Language;
        public string SourceAudioType { get; set; } = Defaults.SourceAudioType;
        public string SourceAudioPath { get; set; } = Defaults.SourceAudioPath;
        public string ProfanityOption { get; set; } = Defaults.ProfanityOption;
        public int InitialSilenceTimeoutMs { get; set; } = Defaults.InitialSilenceTimeoutMs;
        public int EndSilenceTimeoutMs { get; set; } = Defaults.EndSilenceTimeoutMs;
        public int StablePartialResultThreshold { get; set; } = Defaults.StablePartialResultThreshold;
        public bool CaptureAudio {get; set; } = Defaults.CaptureAudio;
    }
}
