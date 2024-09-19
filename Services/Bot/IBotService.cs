using SpeechEnabledCoPilot.Models;

namespace SpeechEnabledCoPilot.Services.Bot
{
    public interface IBotService
    {
        Task<RegionalChannelSettingsDirectLine> GetRegionalChannelSettingsDirectline(string tokenEndpoint)
;
        Task<DirectLineToken> GetTokenAsync(string url);

    }
}