using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechEnabledTvClient .Models
{
    /// <summary>
    /// Represents the settings for the endpointer.
    /// </summary>
    public class EndpointerSettings
    {
        public class Defaults
        {
            public const int StartOfSpeechWindowInMs = 220;
            public const int EndOfSpeechWindowInMs = 900;
        }

        public int StartOfSpeechWindowInMs { get; set; } = Defaults.StartOfSpeechWindowInMs;
        public int EndOfSpeechWindowInMs {get; set; } = Defaults.EndOfSpeechWindowInMs;
    }
}
