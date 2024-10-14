using System;
using System.Timers;

namespace SpeechEnabledCoPilot.Services.Recognizer
{
    public class TimerService
    {
        private System.Timers.Timer _timer;
        public ElapsedEventHandler? _handler;

        public TimerService(double interval)
        {
            _timer = new System.Timers.Timer(interval);
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = false;
        }

        public void Start(ElapsedEventHandler handler)
        {
            _handler = handler;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTimedEvent(object? sender, ElapsedEventArgs e)
        {
            _handler?.Invoke(this, e);
        }
    }
}