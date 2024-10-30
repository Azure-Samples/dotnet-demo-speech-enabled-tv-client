using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpeechEnabledTvClient.Services.Analyzer.EntityAnalyzer
{
  /// <summary>
  /// PromptInput represents the JSON schema for the prompt input to Azure AI completions.
  /// </summary>
  public class PromptInput
  {
    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    [JsonPropertyName("fewShotExamples")]
    public List<FewShotExample> FewShotExamples { get; set; } = new List<FewShotExample>();

    [JsonPropertyName("chatParameters")]
    public ChatParameters ChatParameters { get; set; } = new ChatParameters();
  }

  /// <summary>
  /// FewShotExample represents the JSON schema for the few shot examples in the prompt input to Azure AI completions.
  /// </summary>
  public class FewShotExample
  {
    [JsonPropertyName("chatbotResponse")]
    public string ChatbotResponse { get; set; } = string.Empty;

    [JsonPropertyName("userInput")]
    public string UserInput { get; set; } = string.Empty;
  }

  /// <summary>
  /// ChatParameters represents the JSON schema for the chat parameters in the prompt input to Azure AI completions.
  /// </summary>
  public class ChatParameters
  {
    [JsonPropertyName("deploymentName")]
    public string DeploymentName { get; set; } = string.Empty;

    [JsonPropertyName("maxResponseLength")]
    public int MaxResponseLength { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("topProbablities")]
    public double TopProbabilities { get; set; }

    [JsonPropertyName("stopSequences")]
    public List<string> StopSequences { get; set; } = new List<string>();

    [JsonPropertyName("pastMessagesToInclude")]
    public int PastMessagesToInclude { get; set; }

    [JsonPropertyName("frequencyPenalty")]
    public double FrequencyPenalty { get; set; }

    [JsonPropertyName("presencePenalty")]
    public double PresencePenalty { get; set; }
  }
}