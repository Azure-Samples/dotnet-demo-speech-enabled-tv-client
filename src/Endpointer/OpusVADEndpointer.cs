using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SpeechEnabledTvClient.Models;

namespace SpeechEnabledTvClient.Endpointer
{
    public class OpusVADEndpointer : IEndpointer {
        private ILogger logger;

        // Configuration settings
        private OpusVADConfig config = new OpusVADConfig();
        private OpusVAD.OpusVADOptions options;
        private IntPtr opusVad;
        private int frameSize;
        private int frameBytes;

        // Callback handler
        private IEndpointerHandler? handler;

        // Assigning callbacks to members so that they don't get garbage collected
        private readonly OpusVAD.OpusVadCallback _vadSOS;
        private readonly OpusVAD.OpusVadCallback _vadEOS;

        private bool isInitialized = false;
        private object syncLock = new object();

        /// <summary>
        /// Internal initializer for creating an instance of OpusVAD.
        /// </summary>
        private void init() {
            lock (syncLock)
            {
                if (!isInitialized) {
                    // Create the OpusVAD instance
                    int error;
                    opusVad = OpusVAD.opusvad_create(out error, ref options);
                    if (error != OpusVAD.OPUSVAD_OK)
                    {
                        throw new Exception($"Failed to create OpusVAD. Error: {error}");
                    }

                    // Get the frame size and frame bytes
                    frameSize = OpusVAD.opusvad_get_frame_size(opusVad);
                    frameBytes = frameSize * 2;

                    // Prevent reinitialization
                    isInitialized = true;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpusVADEndpointer"/> class.
        public OpusVADEndpointer(ILogger logger, EndpointerSettings settings) {
            this.logger = logger;

            // Assign the callback handlers so prevent garbage collection
            _vadSOS = new OpusVAD.OpusVadCallback(OnStartOfSpeech);
            _vadEOS = new OpusVAD.OpusVadCallback(OnEndOfSpeech);

            // Set the OpusVAD options
            options = new OpusVAD.OpusVADOptions
            {
                ctx = IntPtr.Zero,
                complexity = config.COMPLEXITY,
                bitRateType = config.BIT_RATE_TYPE,
                sos = settings.StartOfSpeechWindowInMs,
                eos = settings.EndOfSpeechWindowInMs,
                speechDetectionSensitivity = config.SENSITIVITY,
                onSOS = Marshal.GetFunctionPointerForDelegate(_vadSOS),
                onEOS = Marshal.GetFunctionPointerForDelegate(_vadEOS)
            };

            // Initialize the OpusVAD
            init();
        }

        /// <summary>
        /// Starts the OpusVAD endpointer.
        /// </summary>
        /// <param name="handler">The handler to receive endpointer events.</param>
        public void Start(IEndpointerHandler handler) {
            this.handler = handler;
            init(); // This will reinitialize the OpusVAD if it was stopped
        }

        /// <summary>
        /// Stops the OpusVAD endpointer.
        /// </summary>
        public void Stop() {
            lock (syncLock) {
                if (isInitialized) {
                    OpusVAD.opusvad_destroy(opusVad);
                    isInitialized = false;
                }
            }
        }

        /// <summary>
        /// Processes the audio data.
        /// </summary>
        /// <param name="pcm">The PCM audio data.</param>
        /// <exception cref="Exception">Thrown when the OpusVAD is not initialized.</exception>
        /// <returns></returns>
        public void ProcessAudio(byte[] pcm) {
            if (!isInitialized) {
                throw new Exception("OpusVAD not initialized");
            }

            // Process the audio data
            for (int i = 0; i <= ((pcm.Length/2)); i += frameBytes) {
                // Provide an efficient read-only view of the PCM data
                ArraySegment<byte> segment = new ArraySegment<byte>(pcm, i, i+frameBytes);

                // Pass the audio data to the OpusVAD. Any SOS or EOS events will be handled by the callback handlers
                int result = OpusVAD.opusvad_process_audio(opusVad, segment.ToArray(), frameSize);
                if (result != OpusVAD.OPUSVAD_OK)
                {
                    handler?.OnProcessingError(result);
                }
            }
        }

        /// <summary>
        /// Gets the endpointer frame size.
        /// </summary>
        public int GetFrameSize() {
            return frameSize;
        }

        /// <summary>
        /// Callback for the start of speech event.
        /// </summary>
        /// <param name="ptr">The pointer to the OpusVAD instance.</param>
        /// <param name="pos">The position in the audio stream.</param>
        /// <returns></returns>
        /// <remarks>
        /// This method is called by the OpusVAD when the start of speech is detected.
        /// </remarks>
        public void OnStartOfSpeech(IntPtr ptr, uint pos)
        {
            logger.LogDebug($"OpusVad Endpointer onStartOfSpeech: {pos}ms");

            // Notify the handler of the start of speech event
            handler?.OnStartOfSpeech((int)pos);
        }

        /// <summary>
        /// Callback for the end of speech event.
        /// </summary>
        /// <param name="ptr">The pointer to the OpusVAD instance.</param>
        /// <param name="pos">The position in the audio stream.</param>
        /// <returns></returns>
        /// <remarks>
        /// This method is called by the OpusVAD when the end of speech is detected.
        /// </remarks>
        public void OnEndOfSpeech(IntPtr ptr, uint pos)
        {
            logger.LogDebug($"OpusVad Endpointer OnEndOfSpeech: {pos}ms");

            // Notify the handler of the end of speech event
            handler?.OnEndOfSpeech((int)pos);
        }
    }    
}