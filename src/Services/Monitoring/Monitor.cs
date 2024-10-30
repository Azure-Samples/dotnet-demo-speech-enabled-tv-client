using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SpeechEnabledTvClient.Models;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SpeechEnabledTvClient.Monitoring
{
    /// <summary>
    /// Represents the monitor for the application.
    /// </summary>
    public class Monitor
    {
        AppSettings settings;

        private static readonly string NAME = "Microsoft.Demo.DTV";

        // Create default instances of meter and activity source
        public Meter meter = new(NAME, Version.version);
        public ActivitySource activitySource = new(NAME, Version.version);
        
        protected MeterProvider meterProvider { get; set; }
        protected TracerProvider tracerProvider { get; set; }

        protected Histogram<long> NumRequests { get; set; }
        protected Histogram<long> Latency { get; set; }

        public string SessionId { get; set; } = string.Empty;
        public int RequestId { get; set; } = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Monitor"/> class.
        /// </summary>
        /// <param name="settings">The application settings.</param>
        public Monitor(AppSettings settings) {
            this.settings = settings;

            // Create default instances of meterProvider and tracerProvider
            meterProvider = GetMeterProvider(NAME);
            tracerProvider = GetTracerProvider(NAME);

            // Create default histograms
            NumRequests = meter.CreateHistogram<long>("NumRequests");
            Latency = meter.CreateHistogram<long>("Latency");
        }

        /// <summary>
        /// Initializes the monitor.
        /// This method should be called by the service that is being monitored to override defaults.
        /// </summary>
        /// <param name="target">The target of the monitor.</param>
        /// <returns>The monitor instance.</returns>
        public Monitor Initialize(string target) {
            string name = $"{NAME}.{target}";

            // Create instances of meter and activity source specific to the target
            meter = new(name, Version.version);
            activitySource = new(name, Version.version);

            // Create histograms specific to the target
            NumRequests = meter.CreateHistogram<long>($"Num{target}Requests");
            Latency = meter.CreateHistogram<long>($"{target}Latency");

            // Create instances of meterProvider and tracerProvider specific to the target
            meterProvider = GetMeterProvider(name);
            tracerProvider = GetTracerProvider(name);

            return this;
        }

        /// <summary>
        /// Gets the tracer provider.
        /// </summary>
        /// <param name="name">The name of the tracer provider.</param>
        /// <returns>The tracer provider.</returns>
        protected TracerProvider GetTracerProvider(string name) {
            try
            {
                return Sdk.CreateTracerProviderBuilder()
                   .AddSource(name)
                   .AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = settings.AppInsightsConnectionString;
                    })
                   .Build();                
            }
            catch (System.Exception)
            {
                // If the Azure Monitor exporter fails, create a default tracer provider
                return Sdk.CreateTracerProviderBuilder()
                   .AddSource(name)
                   .Build();
            }
        }

        /// <summary>
        /// Gets the meter provider.
        /// </summary>
        /// <param name="name">The name of the meter provider.</param>
        /// <returns>The meter provider.</returns>
        protected MeterProvider GetMeterProvider(string name) {
            try
            {
                return Sdk.CreateMeterProviderBuilder()
                       .AddMeter(name)
                       .AddAzureMonitorMetricExporter(options =>
                        {
                            options.ConnectionString = settings.AppInsightsConnectionString;
                        })
                       .Build();;                
            }
            catch (System.Exception)
            {
                // If the Azure Monitor exporter fails, create a default meter provider
                return Sdk.CreateMeterProviderBuilder()
                       .AddMeter(name)
                       .Build();;
            }
        }
    
        /// <summary>
        /// Disposes of the monitor.
        /// </summary>
        public void Dispose()
        {
            meterProvider?.Dispose();
            tracerProvider?.Dispose();
        }

        /// <summary>
        /// Increments the number of requests.
        /// </summary>
        /// <param name="status">The status of the request.</param>
        public void IncrementRequests(string status)
        {
            NumRequests?.Record(1, new("Status", status), new("SessionId", SessionId), new("RequestId", RequestId));
        }

        /// <summary>
        /// Records the latency of a request.
        /// </summary>
        /// <param name="latency">The latency of the request.</param>
        /// <param name="status">The status of the request.</param>
        public void RecordLatency(long latency, string status)
        {
            Latency?.Record(latency, new("Status", "Success"), new("SessionId", SessionId), new("RequestId", RequestId));
        }
    }
}