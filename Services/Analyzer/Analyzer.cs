using Azure;
using Azure.Core;
using Azure.AI.Language.Conversations;
using Microsoft.Extensions.Logging;
using SpeechEnabledTvClient.Models;
using SpeechEnabledTvClient.Monitoring;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechEnabledTvClient.Services.Analyzer
{
    /// <summary>
    /// Represents the Azure CLU analyzer.
    /// </summary>
    public class Analyzer
    {
        ILogger logger;
        SpeechEnabledTvClient.Monitoring.Monitor monitor;
        Activity? activity;

        AnalyzerSettings settings = AppSettings.AnalyzerSettings();
        private readonly ConversationAnalysisClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="Analyzer"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="monitor">The monitor to use.</param>
        public Analyzer(ILogger logger, SpeechEnabledTvClient.Monitoring.Monitor monitor)
        {
            this.logger = logger;
            this.monitor = monitor.Initialize("CLU");

            Uri uri = new Uri($"https://{settings.CluResource}.cognitiveservices.azure.com/");
            if (string.IsNullOrEmpty(settings.CluKey))
            {
                throw new ArgumentException("CLU key is missing.");
            }
            AzureKeyCredential credential = new AzureKeyCredential(settings.CluKey);
            client = new ConversationAnalysisClient(uri, credential);
        }

        /// <summary>
        /// Creates the input for the analyzer.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private RequestContent createInput(string input)
        {
            using RequestContent content = RequestContent.Create(new
            {
                kind = "Conversation",
                analysisInput = new
                {
                    conversationItem = new
                    {
                        id = "1",
                        text = input,
                        modality = "text",
                        // language = "en-US",
                        participantId = "1",
                    },
                },
                parameters = new
                {
                    projectName = settings.CluProjectName,
                    deploymentName = settings.CluDeploymentName,
                    verbose = true,

                    // Use Utf16CodeUnit for strings in.NET.
                    stringIndexType = "Utf16CodeUnit",
                },
            });
            return content;
        }

        /// <summary>
        /// Analyzes the input.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sessionId"></param>
        /// <returns>AnalyzerResponse</returns>
        public AnalyzerResponse Analyze(string input, string? sessionId = null, int requestId = 0)
        {
            using (activity = monitor.activitySource.StartActivity("Analyze"))
            {
                if (sessionId != null)
                {
                    monitor.SessionId = sessionId;
                }
                monitor.RequestId = requestId;
                activity?.SetTag("SessionId", monitor.SessionId);
                activity?.SetTag("RequestId", monitor.RequestId);
                
                DateTime start = DateTime.Now;
                Response? response = null;
                long latency = 0;
                // Use the client to send the input to the Azure CLU service for analysis.
                try
                {
                    response = client.AnalyzeConversation(createInput(input));
                    latency = (long)(DateTime.Now - start).TotalMilliseconds;

                    if (response.IsError) {
                        ErrorResponse errorResponse = ErrorResponse.FromContentStream(response?.ContentStream);

                        monitor.IncrementRequests("Error");
                        monitor.RecordLatency(latency, "Error");
                        
                        activity?.SetTag("Disposition", "Error");
                        activity?.SetTag("Status", response?.Status.ToString());
                        activity?.SetTag("Reason", response?.ReasonPhrase);
                        activity?.SetTag("ErrorCode", errorResponse.content.error.code);
                        activity?.SetTag("ErrorMessage", errorResponse.content.error.message);

                        return new AnalyzerResponse(errorResponse);
                    }

                    if (!response.IsError) {
                        monitor.IncrementRequests("Success");
                        monitor.RecordLatency(latency, "Success");
                        activity?.SetTag("Disposition", "Success");

                        Interpretation interpration = Interpretation.FromJson(response.Content.ToString());
                        activity?.SetTag("Result", JsonSerializer.Serialize(interpration.result));

                        if (sessionId != null) {
                            logger.LogInformation($"[{sessionId}.{requestId}] Interpretation: {0}", interpration.result.prediction.topIntent);
                        } else {
                            logger.LogInformation("Interpretation: {0}", interpration.result.prediction.topIntent);
                        }
                        
                        return new AnalyzerResponse(interpration);
                    }

                    return new AnalyzerResponse();
                }
                catch (System.Exception ex)
                {
                    latency = (long)(DateTime.Now - start).TotalMilliseconds;

                    if (response == null || response.ContentStream == null) {
                        logger.LogError($"[{sessionId}.{requestId}] Exception returned from the analyzer.");
                        ErrorResponse errorResponse = ErrorResponse.FromContentStream(ex.Message);

                        monitor.IncrementRequests("Error");
                        monitor.RecordLatency(latency, "Error");

                        activity?.SetTag("Disposition", "Error");
                        activity?.SetTag("Status", response?.Status.ToString());
                        activity?.SetTag("Reason", response?.ReasonPhrase);
                        activity?.SetTag("ErrorCode", errorResponse.content.error.code);
                        activity?.SetTag("ErrorMessage", errorResponse.content.error.message);
                        
                        return new AnalyzerResponse(errorResponse);
                    }

                    return new AnalyzerResponse();
                } 
                finally {
                    activity?.SetTag("RequestID", response?.ClientRequestId);
                    activity?.SetTag("Status", response?.Status.ToString());
                    activity?.SetTag("Reason", response?.ReasonPhrase);
                }
            }
        }
    }
}
