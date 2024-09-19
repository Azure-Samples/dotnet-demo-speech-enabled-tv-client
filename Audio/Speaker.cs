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

        BlockingCollection<float[]> dataItems = new BlockingCollection<float[]>();
        float[]? lastSampleArray = null;
        int lastIndex = 0; // not played

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
                param.sampleFormat = SampleFormat.Float32;
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
            // byte[] audioBytes = ConvertToByteArray(audioData);
            using (BinaryWriter binWriter =
                new BinaryWriter(File.Open("audio.pcm", FileMode.Append)))
            {
                binWriter.Write(audioData);
            }
            // int[] samples = new int[audioData.Length / 2];

            // int nBytes = audioData.Length;
            // int i = 0;
            // while (i < nBytes)
            // {
            //     int j = 0;
            //     while (j < 16)
            //     {
            //         if (i == nBytes)
            //         {
            //             break;
            //         }
            //         samples[j] = (short)(audioData[i]);
            //         i++;
            //         samples[j] |= (short)(audioData[i] << 8);
            //         i++;
            //         j++;
            //     }
            // }
            // float[] data = Array.ConvertAll(samples, x => (float)x);
            // dataItems.Add(data);
            // System.Console.WriteLine($"Wrote {audioData.Length} bytes to buffer. Buffer size: {dataItems.Count}");
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
                // float[] samples = new float[frameCount];
                // Marshal.Copy(input, samples, 0, (Int32)frameCount);

                // byte[] audioData = new byte[samples.Length * sizeof(short)];
                // int offset = 0;
                // foreach (float sample in samples)
                // {
                //     short shortValue = (short)(sample * short.MaxValue);
                //     BitConverter.GetBytes(shortValue).CopyTo(audioData, offset);
                //     offset += sizeof(short);
                // }
                // handler.onAudioData(audioData);
                Console.WriteLine("Playing audio...");

                int expected = Convert.ToInt32(frameCount);
                int i = 0;

                while ((lastSampleArray != null || dataItems.Count != 0) && (i < expected))
                {
                    Console.WriteLine("Inside first while loop");
                    int needed = expected - i;

                    if (lastSampleArray != null)
                    {
                        Console.WriteLine("last sample is not null");
                        int remaining = lastSampleArray.Length - lastIndex;
                        if (remaining >= needed)
                        {
                            float[] this_block = lastSampleArray.Skip(lastIndex).Take(needed).ToArray();
                            lastIndex += needed;
                            if (lastIndex == lastSampleArray.Length)
                            {
                                lastSampleArray = null;
                                lastIndex = 0;
                            }

                            Console.WriteLine("copying audio into output [1]...");
                            Marshal.Copy(this_block, 0, IntPtr.Add(output, i * sizeof(float)), needed);
                            return StreamCallbackResult.Continue;
                        }

                        float[] this_block2 = lastSampleArray.Skip(lastIndex).Take(remaining).ToArray();
                        lastIndex = 0;
                        lastSampleArray = null;

                        Console.WriteLine("copying audio into output [2]...");
                        Marshal.Copy(this_block2, 0, IntPtr.Add(output, i * sizeof(float)), remaining);
                        i += remaining;
                        continue;
                    }

                    if (dataItems.Count != 0)
                    {
                        Console.WriteLine("dataItems is not empty. Setting lastSampleArray");
                        lastSampleArray = dataItems.Take();
                        lastIndex = 0;
                    }
                }

                if (i < expected)
                {
                    Console.WriteLine("copying audio into output [3]...");
                    int sizeInBytes = (expected - i) * 4;
                    Marshal.Copy(new byte[sizeInBytes], 0, IntPtr.Add(output, i * sizeof(float)), sizeInBytes);
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
                        stream = new PortAudioSharp.Stream(inParams: null, outParams: param, sampleRate: 16000,
                            framesPerBuffer: 2560,
                            streamFlags: StreamFlags.ClipOff,
                            callback: onPlay,
                            userData: IntPtr.Zero
                        );
                        lastSampleArray = null;
                        lastIndex = 0; // not played

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
