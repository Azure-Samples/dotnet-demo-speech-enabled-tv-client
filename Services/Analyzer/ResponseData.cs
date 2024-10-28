using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpeechEnabledTvClient.Services.Analyzer {

    /// <summary>
    /// Represents the data returned from a prompt completion request.
    /// </summary>
    public class ResponseData {
        [JsonPropertyName("choices")]
        public Choice[] Choices { get; set; } = Array.Empty<Choice>();
    }

    /// <summary>
    /// Represents a choice within the returned data from a prompt completion request.
    /// </summary>
    public class Choice {
        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; } = string.Empty;

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public Message Message { get; set; } = new Message();
    }

    /// <summary>
    /// Represents a message within a choice within the returned data from a prompt completion request.
    /// </summary>
    public class Message {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}