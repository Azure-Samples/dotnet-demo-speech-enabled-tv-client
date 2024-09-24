// TODO: Not working yet

using PortAudioSharp;
// using PortAudioSharp.Enumerations;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Collections.Concurrent;

namespace SpeechEnabledCoPilot.Audio
{
    /// <summary>
    /// Supports the default speaker device as an audio output stream.
    /// </summary>
    public class Speaker : IAudioOutputStream
    {
        IAudioOutputStreamHandler? handler;
        StreamParameters param;
        PortAudioSharp.Stream? stream;
        bool isPlaying = false;
        private object syncLock = new object();

        BlockingCollection<byte[]> dataItems = new BlockingCollection<byte[]>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Speaker"/> class.
        /// </summary>
        public Speaker()
        {
            // Console.WriteLine(PortAudio.VersionInfo.versionText);
            try
            {
                /// Initialize port audio
                PortAudio.Initialize();

                /// Get the default output device
                int deviceIndex = PortAudio.DefaultOutputDevice;
                if (deviceIndex == PortAudio.NoDevice)
                {
                    Console.WriteLine("No default output device found");
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
                Console.WriteLine($"Error initializing microphone: {e.Message}");
            }
        }

        public void onAudioData(byte[] audioData)
        {
            dataItems.Add(audioData);
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
                int i = 0;

                if (dataItems.Count == 0)
                {
                    // Play some silence while we wait for data
                    int sizeInBytes = expected * 2;
                    Marshal.Copy(new byte[sizeInBytes], 0, output, sizeInBytes);
                    return StreamCallbackResult.Continue;
                }

                while ((dataItems.Count != 0) && (i < expected))
                { // Fill up the buffer with the requested audio
                    int needed = expected - i;

                    if (dataItems.Count != 0)
                    {
                        byte[] audio = dataItems.Take();
                        Marshal.Copy(audio, 0, output, audio.Length);
                        i+= audio.Length/2;
                    }
                }
                if (dataItems.Count == 0) { // If we're done we're done
                    return StreamCallbackResult.Complete;
                }
                return StreamCallbackResult.Continue;
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Error recording audio: {e.Message}");
                return StreamCallbackResult.Complete;
            }
        }

        /// <summary>
        /// Starts the audio output stream.
        /// </summary>
        /// <param name="handler">The audio output stream handler.</param>
        public async Task Start(IAudioOutputStreamHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

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
                        stream = new PortAudioSharp.Stream(inParams: null, outParams: param, sampleRate: 48000,
                            framesPerBuffer: 4800,//2560,
                            streamFlags: StreamFlags.ClipOff,
                            callback: onPlay,
                            userData: IntPtr.Zero
                        );

                        stream.Start();
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine($"Error starting speaker: {e.Message}");
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
                        stream.Stop();
                        stream.Dispose();
                        PortAudio.Terminate();
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine($"Error stopping speaker: {e.Message}");
                    }
                    finally
                    {
                        isPlaying = false;
                    }
                }
            }
        }
    }
}
