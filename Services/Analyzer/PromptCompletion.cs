using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using SpeechEnabledTvClient.Models;
using SpeechEnabledTvClient.Monitoring;

namespace SpeechEnabledTvClient.Services.Analyzer
{
    /// <summary>
    /// Represents the Azure AI prompt completion service.
    /// </summary>
    public class PromptCompletion
    {
        ILogger logger;
        SpeechEnabledTvClient.Monitoring.Monitor monitor;
        Activity? activity;

        AnalyzerSettings settings = AppSettings.AnalyzerSettings();

        private readonly HttpClient? _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="PromptCompletion"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="monitor">The monitor to use.</param>
        public PromptCompletion(ILogger logger, SpeechEnabledTvClient.Monitoring.Monitor monitor)
        {
            this.logger = logger;
            this.monitor = monitor.Initialize("OpenAI");

            if (string.IsNullOrEmpty(settings.AzureAIEndpoint))
            {
                logger.LogError("Azure AI endpoint is missing.");
                return;
            }

            if (string.IsNullOrEmpty(settings.AzureAIKey))
            {
                logger.LogError("Azure AI key is missing.");
                return;
            }

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", settings.AzureAIKey);
        }

        /// <summary>
        /// Loads a prompt from file.
        /// </summary>
        /// <param name="promptFile">The name of the prompt file.</param>
        private PromptInput LoadPrompt(string promptFile)
        {
            // Construct the path to the prompt file.
            string promptFilePath = Path.Combine(settings.PromptDir, promptFile);
            if (!File.Exists(promptFilePath))
            {
                throw new FileNotFoundException($"Prompt file not found: {promptFilePath}");
            }

            // Read in the prompt file and deserialize it into a PromptInput object.
            var promptInput = JsonSerializer.Deserialize<PromptInput>(File.ReadAllText(promptFilePath));
            if (promptInput == null)
            {
                throw new Exception("Prompt file is empty or invalid.");
            }
            return promptInput;
        }

        /// <summary>
        /// Creates the payload for the prompt completion request.
        /// </summary>
        /// <param name="promptInput">The prompt input.</param>
        /// <param name="question">The question to ask.</param>
        /// <returns>The payload for the prompt completion request.</returns>
        private object CreatePayload(PromptInput promptInput, string question) {
            // Initialize the messages list with the system prompt.
            var messages = new List<object>
            {
                new {
                    role = "system",
                    content = new object[] {
                        new {
                            type = "text",
                            text = promptInput.SystemPrompt
                        }
                    }
                }
            };

            // Add the few-shot examples to the messages list.
            foreach (var example in promptInput.FewShotExamples)
            {
                messages.Add(new {
                    role = "user",
                    content = new object[] {
                        new {
                            type = "text",
                            text = example.UserInput
                        }
                    }
                });
                messages.Add(new {
                    role = "assistant",
                    content = new object[] {
                        new {
                            type = "text",
                            text = example.ChatbotResponse
                        }
                    }
                });
            }

            // Add the user question to the messages list.
            messages.Add(new {
                role = "user",
                content = new object[] {
                    new {
                        type = "text",
                        text = question
                    }
                }
            });

            // Construct and return the payload object.
            return new
            {
                messages = messages.ToArray(),
                temperature = promptInput.ChatParameters.Temperature,
                top_p = promptInput.ChatParameters.TopProbabilities,
                max_tokens = promptInput.ChatParameters.MaxResponseLength,
                stream = false
            };            
        }

        /// <summary>
        /// Calls Azure AI service and gets the completion of a prompt.
        /// </summary>
        /// <param name="promptFile">The name of the prompt file.</param>
        /// <param name="question">The question to ask.</param>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="requestId">The request ID.</param>
        /// <returns>The prompt completion output.</returns>
        public async Task<PromptOutput> GetPromptCompletion(string promptFile, string question, string? sessionId = null, int requestId = 0)
        {
            if (sessionId != null)
            {
                monitor.SessionId = sessionId;
            }
            monitor.RequestId = requestId;
            
            if (_httpClient == null)
            {
                logger.LogError("HttpClient is not initialized.");
                PromptOutput output = new PromptOutput();
                output.IsError = true;
                output.ReasonPhrase = "HttpClient is not initialized.";
                return output;
            }
            
            DateTime start = DateTime.Now;
            long latency = 0;

            ErrorResponse errorResponse = new ErrorResponse();
            HttpResponseMessage? response = null;
            PromptOutput promptOutput = new PromptOutput();

            using (activity = monitor.activitySource.StartActivity("PromptCompletion"))
            {
                try
                {
                    activity?.SetTag("SessionId", monitor.SessionId);
                    activity?.SetTag("RequestId", monitor.RequestId);

                    // Load the prompt from file and create the payload.
                    PromptInput prompt = LoadPrompt(promptFile);
                    var payload = CreatePayload(prompt, question);

                    // Call the Azure AI service to get the prompt completion.
                    response = await _httpClient.PostAsync(settings.AzureAIEndpoint , new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                    // Measure the latency and set the output properties.
                    latency = (long)(DateTime.Now - start).TotalMilliseconds;
                    promptOutput.IsError = !response.IsSuccessStatusCode;
                    promptOutput.StatusCode = (int)response.StatusCode;

                    // Deserialize the response data if the response is successful.
                    if (response.IsSuccessStatusCode)
                    {
                        var data = JsonSerializer.Deserialize<ResponseData>(await response.Content.ReadAsStringAsync());
                        if (data != null) {
                            promptOutput.ResponseData = data;
                        }
                    }
                    else
                    {
                        // Deserialize the error response if the response is not successful.
                        string? body = response?.Content.ReadAsStringAsync().Result;
                        if (body != null) {
                            errorResponse = ErrorResponse.FromContentStream(body);
                            promptOutput.ReasonPhrase = (response?.ReasonPhrase != null) ? response.ReasonPhrase : "An error occurred while processing the request.";
                        }
                    }
                    return promptOutput;
                }
                catch (System.Exception ex)
                {
                    // Log the exception and set the output properties.
                    if (response == null || response.Content == null) {
                        logger.LogError($"[{sessionId}.{requestId}] Exception returned from PromptCompletion: {ex.Message}");
                        errorResponse = ErrorResponse.FromContentStream(ex.Message);
                    }

                    promptOutput.IsError = true;
                    promptOutput.ReasonPhrase = $"An error occurred while processing the request: {ex.Message}";
                    return promptOutput;
                }
                finally {
                    // Log the prompt completion and update the monitor.
                    string disposition = (promptOutput.IsError) ? "Error" : "Success";
                    string responseData = (promptOutput.ResponseData != null) ? JsonSerializer.Serialize(promptOutput.ResponseData) : "No response data";

                    monitor.IncrementRequests(disposition);
                    monitor.RecordLatency(latency, disposition);

                    activity?.SetTag("Status", response?.StatusCode);
                    activity?.SetTag("Disposition", disposition);
                    activity?.SetTag("Result", responseData);

                    if (promptOutput.IsError) {
                        activity?.SetTag("Reason", response?.ReasonPhrase);
                        activity?.SetTag("ErrorCode", errorResponse.content.error.code);
                        activity?.SetTag("ErrorMessage", errorResponse.content.error.message);
                    }

                    string sanitizedResponseData = responseData.Replace(" ", "")
                                                                .Replace("\\n", "")
                                                                .Replace("\\u0022", "\\\"");
                    if (sessionId != null) {
                        logger.LogInformation($"[{sessionId}.{requestId}] PromptCompletion: {sanitizedResponseData}");
                    } else {
                        logger.LogInformation($"PromptCompletion: {sanitizedResponseData}");
                    }
                }
            }
        }
    }
}