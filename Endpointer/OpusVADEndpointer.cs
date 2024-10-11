using System;
using System.Runtime.InteropServices;

namespace SpeechEnabledCoPilot.Endpointer
{
    public class OpusVADEndpointer : IEndpointer {

        private OpusVADConfig config = new OpusVADConfig();
        private OpusVAD.OpusVADOptions options;
        private IntPtr opusVad;
        private int frameSize;
        private int frameBytes;
        private IEndpointerHandler? handler;
        private OpusVAD.OpusVadCallback _vadSOS;
        private OpusVAD.OpusVadCallback _vadEOS;

        private bool isInitialized = false;
        private object syncLock = new object();

        private void init() {
            lock (syncLock)
            {
                if (!isInitialized) {
                    int error;
                    opusVad = OpusVAD.opusvad_create(out error, ref options);
                    if (error != OpusVAD.OPUSVAD_OK)
                    {
                        throw new Exception($"Failed to create OpusVAD. Error: {error}");
                    }
                    frameSize = OpusVAD.opusvad_get_frame_size(opusVad);
                    frameBytes = frameSize * 2;
                    isInitialized = true;
                }
            }
        }

        public OpusVADEndpointer() {
            _vadSOS = new OpusVAD.OpusVadCallback(OnStartOfSpeech);
            _vadEOS = new OpusVAD.OpusVadCallback(OnEndOfSpeech);
            options = new OpusVAD.OpusVADOptions
            {
                ctx = IntPtr.Zero,
                complexity = config.DEFAULT_COMPLEXITY,
                bitRateType = OpusVAD.OPUSVAD_BIT_RATE_TYPE_CVBR,
                sos = config.SOS_WINDOW_MS,
                eos = config.EOS_WINDOW_MS,
                speechDetectionSensitivity = config.SENSITIVITY,
                onSOS = Marshal.GetFunctionPointerForDelegate((OpusVAD.OpusVadCallback)_vadSOS),
                onEOS = Marshal.GetFunctionPointerForDelegate((OpusVAD.OpusVadCallback)_vadEOS)
            };

            init();
        }

        public void Start(IEndpointerHandler handler) {
            this.handler = handler;
            init();
        }

        public void Stop() {
            lock (syncLock) {
                if (isInitialized) {
                    OpusVAD.opusvad_destroy(opusVad);
                    isInitialized = false;
                }
            }
        }

        public void ProcessAudio(byte[] pcm) {
            if (!isInitialized) {
                throw new Exception("OpusVAD not initialized");
            }

            for (int i = 0; i <= ((pcm.Length/2)-frameBytes); i += frameBytes) {
                ArraySegment<byte> segment = new ArraySegment<byte>(pcm, i, i+frameBytes);
                int result = OpusVAD.opusvad_process_audio(opusVad, segment.ToArray(), frameSize);
                if (result != OpusVAD.OPUSVAD_OK)
                {
                    Console.WriteLine($"Error processing frame. Error: {result}");
                }
            }
        }

        public int GetFrameSize() {
            return frameSize;
        }

        // Callback handlers
        public void OnStartOfSpeech(IntPtr ptr, uint pos)
        {
            Console.WriteLine($"OpusVad Endpointer onStartOfSpeech: {pos}ms");
            if (handler != null) {
                handler.OnStartOfSpeech((int)pos);
            }
        }

        public void OnEndOfSpeech(IntPtr ptr, uint pos)
        {
            Console.WriteLine($"OpusVad Endpointer OnEndOfSpeech: {pos}ms");
            if (handler != null) {
                handler.OnEndOfSpeech((int)pos);
            }
        }
    }    
}