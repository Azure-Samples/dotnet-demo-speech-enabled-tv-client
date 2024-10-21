namespace SpeechEnabledTvClient.Endpointer
{
    /// <summary>
    /// Configuration for the Opus VAD.
    /// </summary>
    public class OpusVADConfig {

        /// <summary>
        /// Default values for the Opus VAD configuration.
        /// </summary>
        public class Defaults {
            public static readonly int FRAME_SIZE_MS = 20;
            public static readonly int LEADING_SILENCE_WINDOW_MS = 120;
            public static readonly int COMPLEXITY = 3;
            public static readonly int BIT_RATE_TYPE = OpusVAD.OPUSVAD_BIT_RATE_TYPE_CVBR;
            public static readonly int SENSITIVITY = 20;
            public static readonly int AUDIO_BUFFER_SIZE = 50 * 2;
        }

        public readonly int FRAME_SIZE_MS = Defaults.FRAME_SIZE_MS;
        public readonly int LEADING_SILENCE_WINDOW_MS = Defaults.LEADING_SILENCE_WINDOW_MS;
        public readonly int COMPLEXITY = Defaults.COMPLEXITY;
        public readonly int BIT_RATE_TYPE = Defaults.BIT_RATE_TYPE;
        public readonly int SENSITIVITY = Defaults.SENSITIVITY;
        public readonly int AUDIO_BUFFER_SIZE = Defaults.AUDIO_BUFFER_SIZE;
    }    
}