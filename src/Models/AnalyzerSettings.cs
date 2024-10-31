using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechEnabledTvClient.Models
{
    /// <summary>
    /// Represents the settings for Azure CLU.
    /// </summary>
    public class AnalyzerSettings
    {
        public class Defaults {
            public const string? CluKey = null;
            public const string? CluResource = null;
            public const string? CluDeploymentName = null;
            public const string? CluProjectName = null;
            public const bool Enable2ndPassCompletion = false;
            public const string? AzureAIEndpoint = null;
            public const string? AzureAIKey = null;
            public const string PromptDir = "resources/prompts/";
            public const string AzureStorageTableUri = null;
        }

        public string? CluKey { get; set; } = Defaults.CluKey;
        public string? CluResource { get; set; } = Defaults.CluResource;
        public string? CluDeploymentName { get; set; } = Defaults.CluDeploymentName;
        public string? CluProjectName { get; set; } = Defaults.CluProjectName;
        public bool Enable2ndPassCompletion { get; set; } = Defaults.Enable2ndPassCompletion;
        public string? AzureAIEndpoint { get; set; } = Defaults.AzureAIEndpoint;
        public string? AzureAIKey { get; set; } = Defaults.AzureAIKey;
        public string PromptDir { get; set; } = Defaults.PromptDir;
        public string AzureStorageTableUri { get; set; } = Defaults.AzureStorageTableUri;
    }
}
