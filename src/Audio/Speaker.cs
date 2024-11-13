using Microsoft.Extensions.Logging;
using PortAudioSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SpeechEnabledTvClient.Audio
{
    /// <summary>
    /// Supports the default speaker device as an audio output stream.
    /// </summary>
    public class Speaker : IAudioOutputStream
    {
        private ILogger logger;

        private IAudioOutputStreamHandler? handler;
        private StreamParameters param;
        private PortAudioSharp.Stream? stream;
        private bool isPlaying = false;
        private object syncLock = new object();
        private int playbackFrequency;
        private BlockingCollection<byte> dataItems = new BlockingCollection<byte>();

        private string sessionId = string.Empty;
        private bool streamStarted = false;

        private readonly Dictionary<string, int> supportedAudioFormats = new Dictionary<string, int>
        {
            {"Raw8Khz16BitMonoPcm", 8000},
            {"Raw16Khz16BitMonoPcm", 16000},
            {"Raw22050Hz16BitMonoPcm", 22050},
            {"Raw24Khz16BitMonoPcm", 24000},
            {"Raw44100Hz16BitMonoPcm", 4100},
            {"Raw48Khz16BitMonoPcm", 48000},
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Speaker"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="outputFormat">The output format to use.</param>
        public Speaker(ILogger logger, string outputFormat)
        {
            this.logger = logger;
            this.streamStarted = false;        
            // Console.WriteLine(PortAudio.VersionInfo.versionText);
            try
            {
                if (!supportedAudioFormats.TryGetValue(outputFormat, out playbackFrequency)) {
                    logger.LogWarning($"WARNING {outputFormat} not supported.  Defaulting to 48K linear 16bit PCM");
                    playbackFrequency = 48000;
                }

                /// Initialize port audio
                PortAudio.Initialize();

                /// Get the default output device
                int deviceIndex = PortAudio.DefaultOutputDevice;
                if (deviceIndex == PortAudio.NoDevice)
                {
                    logger.LogError("No default output device found");
                    throw new Exception("No default output device found");
                }

                /// Get the default output device parameters
                DeviceInfo info = PortAudio.GetDeviceInfo(deviceIndex);

                /// Set the stream parameters
                param = new StreamParameters();
                param.device = deviceIndex;
                param.channelCount = 1;
                param.sampleFormat = SampleFormat.Int16;
                param.suggestedLatency = info.defaultLowInputLatency;
                param.hostApiSpecificStreamInfo = IntPtr.Zero;
            }
            catch (System.Exception e)
            {
                logger.LogError($"Error initializing speaker: {e.Message}");
            }
        }

        /// <summary>
        /// Writes audio data to the audio output stream.
        /// </summary>
        /// <param name="audioData">The audio data to write.</param>
        public void onAudioData(byte[] audioData)
        {
            for (int i = 0; i < audioData.Length; i++)
            {
                dataItems.Add(audioData[i]);
            }
        }

        /// <summary>
        /// PortAudio callback function. Called when audio data is available to be processed.
        /// </summary>
        /// <param name="input">The input buffer (not used for speaker output).</param>
        /// <param name="output">The output buffer.</param>
        /// <param name="frameCount">The number of frames.</param>
        /// <param name="timeInfo">The time information.</param>
        /// <param name="statusFlags">The status flags.</param>
        /// <param name="userData">The user data.</param>
        /// <returns>The stream callback result.</returns> 
        protected StreamCallbackResult onPlay(IntPtr input,
                                IntPtr output,
                                UInt32 frameCount,
                                ref StreamCallbackTimeInfo timeInfo,
                                StreamCallbackFlags statusFlags,
                                IntPtr userData)
        {
            try
            {
                const int bytesPerSample = sizeof(short); // Assuming 16-bit audio samples
                int totalBytesNeeded = (int)frameCount * bytesPerSample;

                // Check if we have enough data to fill the buffer
                if (dataItems.Count < totalBytesNeeded && !streamStarted)
                {
                    // Not enough data; output silence
                    byte[] silenceBuffer = new byte[totalBytesNeeded];
                    Marshal.Copy(silenceBuffer, 0, output, totalBytesNeeded);
                    return StreamCallbackResult.Continue;
                }

                // Prepare the audio buffer
                byte[] audioBuffer = new byte[totalBytesNeeded];
                int bytesRead = 0;

                // Fill the audio buffer with available data
                while (bytesRead < totalBytesNeeded && dataItems.Count > 0)
                {
                    audioBuffer[bytesRead++] = dataItems.Take();
                }
                // If the buffer is not fully filled, pad the rest with silence
                if (bytesRead < totalBytesNeeded)
                {
                    Array.Clear(audioBuffer, bytesRead, totalBytesNeeded - bytesRead);
                }

                // Copy the audio buffer to the output
                Marshal.Copy(audioBuffer, 0, output, totalBytesNeeded);
                streamStarted = true;

                // Check if all data has been played
                if (dataItems.Count == 0)
                {
                    return StreamCallbackResult.Complete;
                }
                return StreamCallbackResult.Continue;
            }
            catch (Exception ex)
            {
                logger.LogError($"[{sessionId}] Error playing audio: {ex.Message}");
                return StreamCallbackResult.Complete;
            }
        }

        /// <summary>
        /// Starts the audio output stream.
        /// </summary>
        /// <param name="handler">The audio output stream handler.</param>
        /// <param name="sessionId">The session ID associated with this start request.</param>
        public async Task Start(IAudioOutputStreamHandler handler, string sessionId = "")
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            this.sessionId = sessionId;

            await Task.Run(() =>
            {
                lock (syncLock)
                {
                    if (isPlaying)
                    {
                        return;
                    }
                    isPlaying = true;
                    this.handler = handler;

                    try
                    {
                        stream = new PortAudioSharp.Stream(inParams: null, outParams: param, 
                            sampleRate: playbackFrequency,
                            framesPerBuffer: (uint)(playbackFrequency / 10), // 100ms intervals
                            streamFlags: StreamFlags.ClipOff,
                            callback: onPlay,
                            userData: IntPtr.Zero
                        );

                        stream.Start();
                        this.handler.onPlayingStarted(sessionId, "Speaker");
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError($"[{sessionId}] Error starting speaker: {e.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Stops the audio output stream.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (stream != null && isPlaying)
                {
                    try
                    {
                        // Wait for the buffer to drain
                        while (dataItems.Count > 0)
                        {
                            Thread.Sleep(100);
                        }
                        stream.Stop();
                        stream.Dispose();
                        PortAudio.Terminate();
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError($"[{sessionId}] Error stopping speaker: {e.Message} {e.StackTrace}");
                    }
                    finally
                    {
                        isPlaying = false;
                        if (handler != null)
                        {
                            handler.onPlayingStopped(sessionId);
                        }
                    }
                }
            }
        }
    }
}
