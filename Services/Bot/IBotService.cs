using SpeechEnabledTvClient .Models;

namespace SpeechEnabledTvClient .Services.Bot
{
    public interface IBotService
    {
        Task<RegionalChannelSettingsDirectLine> GetRegionalChannelSettingsDirectline(string tokenEndpoint)
;
        Task<DirectLineToken> GetTokenAsync(string url);

    }
}