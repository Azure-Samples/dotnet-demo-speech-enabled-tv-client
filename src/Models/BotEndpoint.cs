﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechEnabledTvClient.Models
{
    class BotEndpoint
    {
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="botId">Bot Id GUID</param>
        /// <param name="tenantId">Bot tenant GUID</param>
        /// <param name="tokenEndPoint">REST API endpoint to retreive directline token</param>
        public BotEndpoint(string botId, string tenantId, string tokenEndPoint)
        {
            BotId = botId;
            TenantId = tenantId;
            UriBuilder uriBuilder = new UriBuilder(tokenEndPoint);
            uriBuilder.Query = $"botId={BotId}&tenantId={TenantId}";
            TokenUrl = uriBuilder.Uri;
        }

        public string BotId { get; }

        public string TenantId { get; }

        public Uri TokenUrl { get; }
    }
}
