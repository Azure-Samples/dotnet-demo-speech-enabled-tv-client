using PortAudioSharp;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.IO;

namespace SpeechEnabledCoPilot.Audio
{

    /// <summary>
    /// Supports the default microphone device as an audio input stream.
    /// </summary>
    public class Microphone : IAudioInputStream
    {

        IAudioInputStreamHandler? handler;
        StreamParameters param;
        PortAudioSharp.Stream? stream;
        bool isRecording = false;
        private object syncLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="Microphone"/> class.
        /// </summary>
        public Microphone()
        {
            try
            {
                /// Initialize port audio
                PortAudio.Initialize();

                /// Get the default input device                
                int deviceIndex = PortAudio.DefaultInputDevice;
                if (deviceIndex == PortAudio.NoDevice)
                {
                    Console.WriteLine("No default input device found");
                    throw new Exception("No default input device found");
                }

                /// Get the default input device parameters
                DeviceInfo info = PortAudio.GetDeviceInfo(deviceIndex);

                /// Set the stream parameters
                param = new StreamParameters();
                param.device = deviceIndex;
                param.channelCount = 1;
                param.sampleFormat = SampleFormat.Float32;
                param.suggestedLatency = info.defaultLowInputLatency;
                param.hostApiSpecificStreamInfo = IntPtr.Zero;

            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Error initializing microphone: {e.Message}");
            }
        }

        /// <summary>
        /// PortAudio callback function. Called when audio data is available to be processed.
        /// </summary>
        /// <param name="input">The input buffer.</param>
        /// <param name="output">The output buffer (not used for microphone input).</param>
        /// <param name="frameCount">The number of frames.</param>
        /// <param name="timeInfo">The time information.</param>
        /// <param name="statusFlags">The status flags.</param>
        /// <param name="userData">The user data.</param>
        /// <returns>The stream callback result.</returns>
        protected StreamCallbackResult onRecord(IntPtr input,
                                IntPtr output,
                                UInt32 frameCount,
                                ref StreamCallbackTimeInfo timeInfo,
                                StreamCallbackFlags statusFlags,
                                IntPtr userData)
        {
            try
            {
                /// Copy the audio data to a float array
                float[] samples = new float[frameCount];
                Marshal.Copy(input, samples, 0, (Int32)frameCount);

                /// Convert the float array to a byte array
                byte[] audioData = new byte[samples.Length * sizeof(short)];
                int offset = 0;
                foreach (float sample in samples)
                {
                    /// Convert the float sample to a short and copy to the audio data buffer
                    short shortValue = (short)(sample * short.MaxValue);
                    BitConverter.GetBytes(shortValue).CopyTo(audioData, offset);
                    offset += sizeof(short);
                }
                /// Pass the audio data to the audio input stream handler
                if (handler != null)
                {
                    handler.onAudioData(audioData);
                }
                return StreamCallbackResult.Continue;
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Error recording audio: {e.Message}");
                return StreamCallbackResult.Continue;
            }
        }

        /// <summary>
        /// Starts the audio input stream.
        /// </summary>
        /// <param name="handler">The audio input stream handler.</param>
        public async void Start(IAudioInputStreamHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            await Task.Run(() =>
            {
                lock (syncLock)
                {
                    if (isRecording)
                    {
                        return;
                    }
                    isRecording = true;
                    this.handler = handler;

                    try
                    {
                        /// Start the audio input stream
                        stream = new PortAudioSharp.Stream(inParams: param, outParams: null, sampleRate: 16000,
                            framesPerBuffer: 2560,
                            streamFlags: StreamFlags.ClipOff,
                            callback: onRecord,
                            userData: IntPtr.Zero
                        );
                        stream.Start();
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine($"Error starting microphone: {e.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Stops the audio input stream.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (stream != null && isRecording)
                {
                    try
                    {
                        stream.Stop();
                        stream.Dispose();
                        PortAudio.Terminate();
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine($"Error stopping microphone: {e.Message}");
                    }
                    finally
                    {
                        isRecording = false;
                    }
                }
            }
        }
    }
}
