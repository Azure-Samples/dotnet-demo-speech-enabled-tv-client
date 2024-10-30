using Azure;
using Azure.Core;
using Azure.AI.Language.Conversations;
using Microsoft.Extensions.Logging;
using SpeechEnabledTvClient.Models;
using SpeechEnabledTvClient.Monitoring;
using SpeechEnabledTvClient.Services.Analyzer.EntityAnalyzer;
using SpeechEnabledTvClient.Services.Analyzer.EntityModels;
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
        private readonly PromptCompletion? promptCompletion;

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

            // Initialize the prompt completion service if enabled.
            if (settings.Enable2ndPassCompletion)
            {
                promptCompletion = new PromptCompletion(logger, new SpeechEnabledTvClient.Monitoring.Monitor(AppSettings.LoadAppSettings(new string[] { })));
            }
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
        /// Builds a response from an Analyzer service error response.
        /// </summary>
        /// <param name="response">The response from the Analyzer service.</param>
        /// <returns>AnalyzerResponse</returns>
        private AnalyzerResponse BuildResponseFromError(Response? response)
        {
            // Read the error response from the content stream.
            ErrorResponse errorResponse = ErrorResponse.FromContentStream(response?.ContentStream);

            // monitor and log error details
            monitor.IncrementRequests("Error");
            
            activity?.SetTag("Disposition", "Error");
            activity?.SetTag("ErrorCode", errorResponse.content.error.code);
            activity?.SetTag("ErrorMessage", errorResponse.content.error.message);

            // Return the error response.
            return new AnalyzerResponse(errorResponse);
        }

        /// <summary>
        /// Processes the calendarx entity.
        /// </summary>
        /// <param name="entityText">The entity text to process.</param>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="requestId">The request ID.</param>
        /// <returns>NuanceCalendarOption</returns>
        /// <remarks>
        /// This method provides a simple example of applying 2nd pass prompt completion against an entity
        /// that, with legancy Nuance NLUaaS, provided a more detailed breakdown of the entity.
        /// </remarks>
        private NuanceCalendarOption ProcessCalendarXEntity(string entityText, string? sessionId, int requestId)
        {
            // safety check...
            if (promptCompletion == null)
            {
                return new NuanceCalendarOption();
            }

            // Use the prompt completion service to get the calendarx entity details.
            PromptOutput promptOutput = promptCompletion.GetPromptCompletion("calendarx.json", entityText, sessionId, requestId).Result;
            if (!promptOutput.IsError)
            {
                // Deserialize the calendarx entity details.
                var calData = JsonSerializer.Deserialize<NuanceCalendarOption>(promptOutput.ResponseData.Choices[0].Message.Content);
                if (calData != null)
                {
                    return calData;
                }
            } else {
                logger.LogError($"PromptOutput: {promptOutput.ReasonPhrase}");
            }
            return new NuanceCalendarOption();
        }

        /// <summary>
        /// Processes the entities.
        /// </summary>
        /// <param name="entities">The entities to process.</param>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="requestId">The request ID.</param>
        /// <returns>Entity[]</returns>
        /// <remarks>
        /// This method loops through the entities and applies additional processing to the calendarx entity.
        /// </remarks>
        private Entity[] ProcessEntities(Entity[] entities, string? sessionId, int requestId)
        {
            // ignore if 2nd pass completion is not enabled
            if (!settings.Enable2ndPassCompletion)
            {
                return entities;
            }

            // for each entity, check for and process calendarx entities
            int entityCount = 0;
            foreach (var entity in entities)
            {
                if (entity.category == "nuance_CALENDARX")
                {
                    logger.LogInformation($"Found nuance_CALENDARX entity: {entity.text}");
                    entities[entityCount].nuance_CALENDARX = ProcessCalendarXEntity(entity.text, sessionId, requestId);
                }
                entityCount++;
            }
            return entities;
        }

        /// <summary>
        /// Builds a response from an Analyzer service successful response.
        /// </summary>
        /// <param name="response">The response from the Analyzer service.</param>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="requestId">The request ID.</param>
        /// <returns>AnalyzerResponse</returns>
        private AnalyzerResponse BuildResponseFromResponse(Response response, string? sessionId, int requestId)
        {
            // monitor some stuff
            monitor.IncrementRequests("Success");
            activity?.SetTag("Disposition", "Success");

            // Deserialize the response content.
            Interpretation interpration = Interpretation.FromJson(response.Content.ToString());
            activity?.SetTag("Result", JsonSerializer.Serialize(interpration.result));

            // Log the interpretation.
            if (sessionId != null) {
                logger.LogInformation($"[{sessionId}.{requestId}] Interpretation: {interpration.result.prediction.topIntent}");
            } else {
                logger.LogInformation($"Interpretation: {interpration.result.prediction.topIntent}");
            }

            // Process the entities.
            interpration.result.prediction.entities = ProcessEntities(interpration.result.prediction.entities, sessionId, requestId);
            
            return new AnalyzerResponse(interpration);
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

                try
                {
                    // Use the client to send the input to the Azure CLU service for analysis.
                    RequestContext ctx = new RequestContext();
                    ctx.ErrorOptions = ErrorOptions.NoThrow;
                    response = client.AnalyzeConversation(createInput(input), ctx);
                    latency = (long)(DateTime.Now - start).TotalMilliseconds;

                    // Check for an error response.
                    if (response.IsError) {
                        monitor.RecordLatency(latency, "Error");
                        return BuildResponseFromError(response);
                    }

                    // Build the response from the successful response.
                    monitor.RecordLatency(latency, "Success");
                    return BuildResponseFromResponse(response, sessionId, requestId);
                }
                catch (System.Exception ex)
                {
                    latency = (long)(DateTime.Now - start).TotalMilliseconds;

                    logger.LogError($"[{sessionId}.{requestId}] Exception returned from the analyzer.");
                    monitor.IncrementRequests("Error");
                    monitor.RecordLatency(latency, "Error");
                    activity?.SetTag("Disposition", "Error");

                    ErrorResponse errorResponse = ErrorResponse.FromContentStream(ex.Message);
                    activity?.SetTag("ErrorCode", errorResponse.content.error.code);
                    activity?.SetTag("ErrorMessage", errorResponse.content.error.message);
                    
                    return new AnalyzerResponse(errorResponse);
                } 
                finally {
                    activity?.SetTag("ClientRequestID", response?.ClientRequestId);
                    activity?.SetTag("Status", response?.Status.ToString());
                    activity?.SetTag("Reason", response?.ReasonPhrase);
                }
            }
        }
    }
}
