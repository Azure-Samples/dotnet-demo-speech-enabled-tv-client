using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechEnabledCoPilot.Models
{
    public class BotSettings
    {
        public class Defaults {
            public const string? BotId = null;
            public const string? BotTenantId = null;
            public const string? BotName = null;
            public const string? BotTokenEndpoint = null;
            public const string? EndConversationMessage = "quit";
        }

        public string? BotId { get; set; } = Defaults.BotId;
        public string? BotTenantId { get; set; } = Defaults.BotTenantId;
        public string? BotName { get; set; } = Defaults.BotName;
        public string? BotTokenEndpoint { get; set; } = Defaults.BotTokenEndpoint;
        public string? EndConversationMessage { get; set; } = Defaults.EndConversationMessage;
    }
}
