using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SpeechEnabledTvClient .Audio
{
    /// <summary> 
    /// Represents an audio file that can be written to.
    /// </summary>
    public class AudioFile : IAudioOutputStream
    {
        private ILogger logger;

        private readonly string directory;
        private IAudioOutputStreamHandler? handler;
        private BinaryWriter? binWriter;
        private bool isPlaying = false;
        private object syncLock = new object();

        private string sessionId = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioFile"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public AudioFile(ILogger logger) : this(logger, null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioFile"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="directory">The directory to save the audio file.</param>
        public AudioFile(ILogger logger, string? directory)
        {
            this.logger = logger;
            this.directory = directory ?? "./";
            createDirectory(this.directory);
        }

        /// <summary>
        /// Creates a directory if it does not exist.
        /// </summary>
        /// <param name="directory">The directory to create.</param>
        private void createDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Writes audio data to the audio file.
        /// </summary>
        /// <param name="audioData">The audio data to write.</param>
        public void onAudioData(byte[] audioData)
        {
            if (binWriter != null)
            {
                binWriter.Write(audioData);
            }
        }

        /// <summary>
        /// Starts writing audio data to the audio file.
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
                /// If already playing, return.
                lock (syncLock)
                {
                    if (isPlaying)
                    {
                        return;
                    }

                    /// Set the handler and start playing.
                    isPlaying = true;
                    this.handler = handler;
                    
                    /// Create a new audio file using the current timestamp.
                    long fileId = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                    string filePath = Path.Combine(directory, $"audio-{fileId}.pcm");
                    
                    binWriter = new BinaryWriter(File.Open(filePath, FileMode.Create));
                    handler.onPlayingStarted(sessionId, filePath);
                }
            });
        }

        /// <summary>
        /// Stops writing audio data to the audio file.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (binWriter != null && isPlaying)
                {
                    /// Flush and close the binary writer.
                    binWriter.Flush();
                    binWriter.Close();
                    isPlaying = false;

                    /// Notify the handler that playing has stopped.
                    if (handler != null)
                    {
                        handler.onPlayingStopped(sessionId);
                    }
                }
            }
        }
    }
}
