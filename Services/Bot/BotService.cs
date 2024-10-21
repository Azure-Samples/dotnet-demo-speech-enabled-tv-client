using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using SpeechEnabledTvClient.Models;

namespace SpeechEnabledTvClient.Services.Bot
{
    public class BotService : IBotService
    {
        private HttpClient _httpClient;

        public BotService() 
        {
            _httpClient = new HttpClient();
        }

        public async Task<RegionalChannelSettingsDirectLine> GetRegionalChannelSettingsDirectline(string tokenEndpoint)
        { 
            string environmentEndPoint = tokenEndpoint.Substring(0, tokenEndpoint.IndexOf("/powervirtualagents/"));
            string apiVersion = tokenEndpoint.Substring(tokenEndpoint.IndexOf("api-version")).Split("=")[1];
            var regionalChannelSettingsURL = $"{environmentEndPoint}/powervirtualagents/regionalchannelsettings?api-version={apiVersion}";

            try
            {
                var regionalSettings = await _httpClient.GetFromJsonAsync<RegionalChannelSettingsDirectLine>(regionalChannelSettingsURL);
                if (regionalSettings == null)
                {
                    throw new HttpRequestException("Failed to get regional channel settings");
                }
                return regionalSettings;
            }
            catch (HttpRequestException ex)
            {
                #pragma warning disable CA2200                
                throw ex;
                #pragma warning disable CA2200
            }
            
        }
            

        public async Task<DirectLineToken> GetTokenAsync(string url)
        {
            try
            {
                var token = await _httpClient.GetFromJsonAsync<DirectLineToken>(url);
                if (token == null)
                {
                    throw new HttpRequestException("Failed to get token");
                }
                return token;
            }
            catch (HttpRequestException ex)
            {
                throw ex;
            }        
        }
    }
}
