using System;
using System.Timers;

namespace SpeechEnabledTvClient.Services.Recognizer
{
    /// <summary>
    /// Represents the event handler for the timer service.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="e">The event arguments.</param>
    /// <returns></returns>
    /// <remarks>Used to handle timer expired events.</remarks>
    public delegate void TimerServiceEventHandler(object? sender, TimerServiceEventArgs e);

    /// <summary>
    /// Represents the event arguments for the timer service.
    /// </summary>
    /// <remarks>Used to pass the session ID and signal time to the event handler.</remarks>
    public class TimerServiceEventArgs : EventArgs {
        public string SessionId { get; }
        public DateTime SignalTime { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimerServiceEventArgs"/> class.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="signalTime">The signal time.</param>
        public TimerServiceEventArgs(string sessionId, DateTime signalTime) {
            SessionId = sessionId;
            SignalTime = signalTime;
        }
    }
    
    /// <summary>
    /// Represents the timer service.
    /// </summary>
    /// <remarks>Used to create and manage a timer service.</remarks>
    public class TimerService
    {
        private System.Timers.Timer _timer;
        private TimerServiceEventHandler? _handler;
        private string _sessionId = "00000000-0000-0000-0000-000000000000";

        /// <summary>
        /// Initializes a new instance of the <see cref="TimerService"/> class.
        /// </summary>
        /// <param name="interval">The interval, in milliseconds, for the timer.</param>
        public TimerService(double interval)
        {
            _timer = new System.Timers.Timer(interval);
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = false;
        }

        /// <summary>
        /// Starts the timer service.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="handler">The event handler.</param>
        /// <returns></returns>
        public void Start(string sessionId, TimerServiceEventHandler handler)
        {
            _sessionId = sessionId;
            _handler = handler;
            _timer.Start();
        }

        /// <summary>
        /// Stops the timer service.
        /// </summary>
        /// <returns></returns>
        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>
        /// Handles the timer event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <returns></returns>
        /// <remarks>Invokes the event handler with the session ID and signal time.</remarks>
        private void OnTimedEvent(object? sender, ElapsedEventArgs e)
        {
            TimerServiceEventArgs args = new TimerServiceEventArgs(_sessionId, e.SignalTime);
            _handler?.Invoke(this, args);
        }
    }
}