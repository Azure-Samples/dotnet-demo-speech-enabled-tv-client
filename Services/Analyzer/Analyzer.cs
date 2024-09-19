using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Azure.Core;
using Azure;
using Azure.AI.Language.Conversations;
using SpeechEnabledCoPilot.Models;

namespace SpeechEnabledCoPilot.Services.Analyzer
{
    /// <summary>
    /// Represents the Azure CLU analyzer.
    /// </summary>
    public class Analyzer
    {
        AnalyzerSettings settings = AppSettings.AnalyzerSettings();
        private readonly ConversationAnalysisClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="Analyzer"/> class.
        /// </summary>
        public Analyzer()
        {
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

                    // Use Utf16CodeUnit for strings in .NET.
                    stringIndexType = "Utf16CodeUnit",
                },
            });
            return content;
        }

        /// <summary>
        /// Analyzes the input.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public JsonElement Analyze(string input)
        {
            // Use the client to send the input to the Azure CLU service for analysis.
            Response response = client.AnalyzeConversation(createInput(input));
            if (response == null || response.ContentStream == null) {
                throw new Exception("No response from the analyzer.");
            }

            // Parse the response to get the result.
            JsonDocument result = JsonDocument.Parse(response.ContentStream);
            JsonElement conversationalTaskResult = result.RootElement;
            return conversationalTaskResult.GetProperty("result");
        }
    }
}
