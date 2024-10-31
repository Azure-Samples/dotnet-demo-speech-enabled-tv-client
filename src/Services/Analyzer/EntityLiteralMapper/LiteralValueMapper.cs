using Azure;
using Azure.Identity;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SpeechEnabledTvClient.Services.Analyzer.EntityLiteralMapper
{
    /// <summary>
    /// Represents mappings of entity names returned from CLU to storage table names containing literal/value mappings
    /// </summary>
    public static class EntityTableMappings
    {
        // key = entity name (ie. category) returned from CLU
        // value = storage table name (this can be whatever you want)
        public static readonly Dictionary<string, string> EntityTableNameMappings = new Dictionary<string, string>
        {
            { "dtv_nuance_tv_station", "dtvNuanceTvStationLVMappings" },
            // { "dtv_nuance_tv_media_type", "dtvNuanceTvMediaTypeLVMappings" },
            // { "dtv_nuance_tv_media_genre", "dtvNuanceTvMediaGenraLVMappings" }
            // Add more mappings as needed
        };
    }

    /// <summary>
    /// Represents a single literal/value mapping
    /// </summary>
    /// <remarks>
    /// RowKey represents the literal, Value is the mapped value
    /// LanguageCode is a placeholder for filtering on language if desired
    /// </remarks>
    public class LiteralValueMapping : ITableEntity
    {
        public Azure.ETag ETag { get; set; } = new ETag(string.Empty);
        public DateTimeOffset? Timestamp { get; set; }
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a locally cached representation of literal/value mappings for a given entity.
    /// </summary>
    public class LiteralValueMapper
    {
        private readonly ILogger logger;
        private readonly string _entity;
        private readonly string _languageCode;
        private readonly string _tableName;
        private readonly TableClient? _tableClient;
        private Dictionary<string, LiteralValueMapping> _mappings = new Dictionary<string, LiteralValueMapping>();

        /// <summary>
        /// Initializes a new instance of the <see cref="LiteralValueMapper"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging errors and information.</param>
        /// <param name="storageUri">The URI of the Azure storage account or "UseDevelopmentStorage=true" for development storage.</param>
        /// <param name="tableName">The name of the Azure Table to query for literal value mappings.</param>
        /// <param name="entity">The entity name used as the partition key in the Azure Table.</param>
        /// <param name="languageCode">The language code associated with the entity.</param>
        public LiteralValueMapper(ILogger logger, string storageUri, string tableName, string entity, string languageCode)
        {
            this.logger = logger;
            _entity = entity;
            _languageCode = languageCode;
            _tableName = tableName;

            try {
                if (storageUri == "UseDevelopmentStorage=true") {
                    _tableClient = new TableClient(storageUri, tableName);
                } else {
                    _tableClient = new TableClient(
                        new Uri(storageUri),
                        tableName,
                        new DefaultAzureCredential());
                }
                            
                _mappings = LoadMappings().GetAwaiter().GetResult();
            } catch(Exception ex) {
                logger.LogError($"Error creating Azure storage client: {ex.Message}");
            }
        }


        /// <summary>
        /// Asynchronously loads the literal value mappings from the Azure Table storage.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a dictionary
        /// where the keys are the row keys and the values are the corresponding <see cref="LiteralValueMapping"/> objects.
        /// </returns>
        /// <remarks>
        /// This method queries the Azure Table storage for entries that match the specified entity (partition key).
        /// If the table client is not initialized, an empty dictionary is returned.
        /// In case of any exceptions during the query, the error is logged and an empty dictionary is returned.
        /// </remarks>        
        private async Task<Dictionary<string, LiteralValueMapping>> LoadMappings()
        {
            Dictionary<string, LiteralValueMapping> mappings = new Dictionary<string, LiteralValueMapping>();
            if (_tableClient == null) {
                return mappings;
            }

            try
            {
                // Load mappings from the database
                AsyncPageable<LiteralValueMapping> results = _tableClient.QueryAsync<LiteralValueMapping>(
                    mapping => mapping.PartitionKey == _entity
                );

                int resultCount = 0;
                await foreach (LiteralValueMapping result in results)
                {
                    // should we make lookup key case-insensitive?
                    mappings.Add(result.RowKey, result);
                    resultCount++;
                }
                // logger.LogInformation($"Entity {_entity} number of literal/value mappings: {resultCount}");

                return mappings;
            }
            catch (RequestFailedException rfe)
            {
                logger.LogError($"Error fetching literal/value mappings [{_entity} : {_tableName}]: {rfe.Status}: {rfe.ErrorCode}");
                return mappings;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching literal/value mappings [{_entity} : {_tableName}]: {ex.Message}");
                return mappings;
            }
        }

        /// <summary>
        /// Retrieves the mapped value for a given literal.
        /// </summary>
        /// <param name="literal">The literal string to look up in the mappings.</param>
        /// <returns>
        /// The mapped value if the literal exists in the mappings; otherwise, returns the original literal.
        /// </returns>
        public string GetMappedValue(string literal)
        {
            if (_mappings.ContainsKey(literal))
            {
                return _mappings[literal].Value;
            }
            return literal;
        }
    }
}