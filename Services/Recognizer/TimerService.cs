using System;
using System.Timers;

namespace SpeechEnabledCoPilot.Services.Recognizer
{
    public delegate void TimerServiceEventHandler(object? sender, TimerServiceEventArgs e);

    public class TimerServiceEventArgs : EventArgs {
        public string SessionId { get; }
        public DateTime SignalTime { get; }

        public TimerServiceEventArgs(string sessionId, DateTime signalTime) {
            SessionId = sessionId;
            SignalTime = signalTime;
        }
    }
    
    public class TimerService
    {
        private System.Timers.Timer _timer;
        private TimerServiceEventHandler? _handler;
        private string _sessionId = "00000000-0000-0000-0000-000000000000";
                
        public TimerService(double interval)
        {
            _timer = new System.Timers.Timer(interval);
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = false;
        }

        public void Start(string sessionId, TimerServiceEventHandler handler)
        {
            _sessionId = sessionId;
            _handler = handler;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTimedEvent(object? sender, ElapsedEventArgs e)
        {
            TimerServiceEventArgs args = new TimerServiceEventArgs(_sessionId, e.SignalTime);
            _handler?.Invoke(this, args);
        }
    }
}