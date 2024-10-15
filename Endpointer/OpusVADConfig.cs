namespace SpeechEnabledCoPilot.Endpointer
{
    public class OpusVADConfig {

        public class Defaults {
            public static readonly int FRAME_SIZE_MS = 20;
            public static readonly int LEADING_SILENCE_WINDOW_MS = 120;
            public static readonly int SOS_WINDOW_MS = 220;
            public static readonly int EOS_WINDOW_MS = 1100;
            public static readonly int DEFAULT_COMPLEXITY = 3;
            public static readonly int SENSITIVITY = 20;
            public static readonly int AUDIO_BUFFER_SIZE = 50 * 2;
        }

        public readonly int FRAME_SIZE_MS = Defaults.FRAME_SIZE_MS;
        public readonly int LEADING_SILENCE_WINDOW_MS = Defaults.LEADING_SILENCE_WINDOW_MS;
        public readonly int SOS_WINDOW_MS = Defaults.SOS_WINDOW_MS;
        public readonly int EOS_WINDOW_MS = Defaults.EOS_WINDOW_MS;
        public readonly int DEFAULT_COMPLEXITY = Defaults.DEFAULT_COMPLEXITY;
        public readonly int SENSITIVITY = Defaults.SENSITIVITY;
        public readonly int AUDIO_BUFFER_SIZE = Defaults.AUDIO_BUFFER_SIZE;
    }    
}