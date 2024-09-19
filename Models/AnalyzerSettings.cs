using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechEnabledCoPilot.Models
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
        }

        public string? CluKey { get; set; } = Defaults.CluKey;
        public string? CluResource { get; set; } = Defaults.CluResource;
        public string? CluDeploymentName { get; set; } = Defaults.CluDeploymentName;
        public string? CluProjectName { get; set; } = Defaults.CluProjectName;
    }
}
