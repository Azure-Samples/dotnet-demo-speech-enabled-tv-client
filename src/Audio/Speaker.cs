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
            // Console.WriteLine(PortAudio.VersionInfo.versionText);    
            this.streamStarted = false;        
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
                int expected = Convert.ToInt32(frameCount);

                if (dataItems.Count < frameCount/sizeof(Int16) && !streamStarted)
                {
                    // Play some silence while we wait for data
                    int sizeInBytes = expected * sizeof(Int16);
                    Marshal.Copy(new byte[sizeInBytes], 0, output, sizeInBytes);
                    return StreamCallbackResult.Continue;
                } 
                else {
                    byte[] audio = new byte[frameCount*sizeof(Int16)];
                    for (int i = 0; dataItems.Count > 0 && i < frameCount*sizeof(Int16); i+=sizeof(Int16))
                    {
                        audio[i] = dataItems.Take();
                        audio[i+1] = dataItems.Take();
                    }
                    Marshal.Copy(audio, 0, output, audio.Length);
                    streamStarted = true;
                }

                // If we're done we're done
                if (dataItems.Count == 0)
                {
                    return StreamCallbackResult.Complete;
                }

                return StreamCallbackResult.Continue;
            }
            catch (System.Exception e)
            {
                logger.LogError($"[{sessionId}] Error playing audio: {e.Message}");
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
