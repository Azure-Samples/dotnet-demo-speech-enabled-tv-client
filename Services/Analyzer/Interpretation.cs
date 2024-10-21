// This code was auto-generated using https://app.quicktype.io/
#nullable enable
#pragma warning disable CS8618
#pragma warning disable CS8601
#pragma warning disable CS8603

namespace SpeechEnabledTvClient.Services.Analyzer
{
    using System;
    using System.Collections.Generic;

    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Globalization;

    /// <summary>
    /// Represents the interpretation of the input text.
    /// </summary>
    public partial class Interpretation
    {
        [JsonPropertyName("kind")]
        public string kind { get; set; }

        [JsonPropertyName("result")]
        public Result result { get; set; }
    }

    /// <summary>
    /// Represents the result of the interpretation.
    /// </summary>
    public partial class Result
    {
        [JsonPropertyName("query")]
        public string query { get; set; }

        [JsonPropertyName("prediction")]
        public Prediction prediction { get; set; }
    }

    /// <summary>
    /// Represents the prediction of the interpretation.
    /// </summary>
    public partial class Prediction
    {
        [JsonPropertyName("topIntent")]
        public string topIntent { get; set; }

        [JsonPropertyName("projectKind")]
        public string projectKind { get; set; }

        [JsonPropertyName("intents")]
        public Intent[] intents { get; set; }

        [JsonPropertyName("entities")]
        public Entity[] entities { get; set; }
    }

    /// <summary>
    /// Represents an entity in the interpretation.
    /// </summary>
    public partial class Entity
    {
        [JsonPropertyName("category")]
        public string category { get; set; }

        [JsonPropertyName("text")]
        public string text { get; set; }

        [JsonPropertyName("offset")]
        public long offset { get; set; }

        [JsonPropertyName("length")]
        public long length { get; set; }

        [JsonPropertyName("confidenceScore")]
        public long confidenceScore { get; set; }

        [JsonPropertyName("resolutions")]
        public Resolution[] resolutions { get; set; }

        [JsonPropertyName("extraInformation")]
        public ExtraInformation[] extraInformation { get; set; }
    }

    /// <summary>
    /// Represents extra information in the interpretation.
    /// </summary>
    public partial class ExtraInformation
    {
        [JsonPropertyName("extraInformationKind")]
        public string extraInformationKind { get; set; }

        [JsonPropertyName("value")]
        public string value { get; set; }
    }

    /// <summary>
    /// Represents a resolution in the interpretation.
    /// </summary>
    public partial class Resolution
    {
        [JsonPropertyName("resolutionKind")]
        public string resolutionKind { get; set; }

        [JsonPropertyName("dateTimeSubKind")]
        public string dateTimeSubKind { get; set; }

        [JsonPropertyName("timex")]
        public string timex { get; set; }

        [JsonPropertyName("value")]
        public DateTimeOffset value { get; set; }
    }

    /// <summary>
    /// Represents an intent in the interpretation.
    /// </summary>
    public partial class Intent
    {
        [JsonPropertyName("category")]
        public string category { get; set; }

        [JsonPropertyName("confidenceScore")]
        public double confidenceScore { get; set; }
    }

    /// <summary>
    /// Represents the interpretation of the input text.
    /// </summary>
    public partial class Interpretation
    {
        public static Interpretation FromJson(string json) => JsonSerializer.Deserialize<Interpretation>(json, SpeechEnabledTvClient.Services.Analyzer.Converter.Settings);
    }

    /// <summary>
    /// Represents the serialized json representation of the interpretation.
    /// </summary>
    public static class Serialize
    {
        public static string ToJson(this Interpretation self) => JsonSerializer.Serialize(self, SpeechEnabledTvClient.Services.Analyzer.Converter.Settings);
    }

    /// <summary>
    /// Represents the JSON converter for the interpretation.
    /// </summary>
    /// <remarks>
    /// This class is used to convert the interpretation to and from JSON.
    /// </remarks>
    internal static class Converter
    {
        public static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.General)
        {
            Converters =
            {
                new DateOnlyConverter(),
                new TimeOnlyConverter(),
                IsoDateTimeOffsetConverter.Singleton
            },
        };
    }
    
    /// <summary>
    /// Represents a date-only JSON converter.
    /// </summary>
    /// <remarks>
    /// This class is used to convert a date-only object to and from JSON.
    /// </remarks>
    public class DateOnlyConverter : JsonConverter<DateOnly>
    {
        private readonly string serializationFormat;
        public DateOnlyConverter() : this(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DateOnlyConverter"/> class with the specified serialization format.
        /// </summary>
        /// <param name="serializationFormat">The serialization format.</param>
        public DateOnlyConverter(string? serializationFormat)
        {
            this.serializationFormat = serializationFormat ?? "yyyy-MM-dd";
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">The json serializer options.</param>
        /// <returns>The deserialized object.</returns>
        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return DateOnly.Parse(value!);
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The writer to use.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="options">The json serializer options.</param>        
        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString(serializationFormat));
    }

    /// <summary>
    /// Represents a time-only JSON converter.
    /// </summary> 
    /// <remarks>
    /// This class is used to convert a time-only object to and from JSON.
    /// </remarks>    
    public class TimeOnlyConverter : JsonConverter<TimeOnly>
    {
        private readonly string serializationFormat;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeOnlyConverter"/> class.
        /// </summary>
        public TimeOnlyConverter() : this(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeOnlyConverter"/> class with the specified serialization format.
        /// </summary>
        /// <param name="serializationFormat">The serialization format.</param>
        /// <remarks>
        /// The default serialization format is "HH:mm:ss.fff".
        /// </remarks>
        public TimeOnlyConverter(string? serializationFormat)
        {
                this.serializationFormat = serializationFormat ?? "HH:mm:ss.fff";
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">The json serializer options.</param>
        /// <returns>The deserialized object.</returns>
        public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
                var value = reader.GetString();
                return TimeOnly.Parse(value!);
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The writer to use.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="options">The json serializer options.</param>
        public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString(serializationFormat));
    }

    /// <summary>
    /// Represents an ISO date-time offset JSON converter.
    /// </summary>
    /// <remarks>
    /// This class is used to convert an ISO date-time offset object to and from JSON.
    /// </remarks>
    internal class IsoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        /// <summary>
        /// Determines whether the specified type can be converted.
        /// </summary>
        public override bool CanConvert(Type t) => t == typeof(DateTimeOffset);

        private const string DefaultDateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";

        private DateTimeStyles _dateTimeStyles = DateTimeStyles.RoundtripKind;
        private string? _dateTimeFormat;
        private CultureInfo? _culture;

        public DateTimeStyles DateTimeStyles
        {
                get => _dateTimeStyles;
                set => _dateTimeStyles = value;
        }

        public string? DateTimeFormat
        {
                get => _dateTimeFormat ?? string.Empty;
                set => _dateTimeFormat = (string.IsNullOrEmpty(value)) ? null : value;
        }

        public CultureInfo Culture
        {
                get => _culture ?? CultureInfo.CurrentCulture;
                set => _culture = value;
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The writer to use.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="options">The json serializer options.</param>
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
                string text;


                if ((_dateTimeStyles & DateTimeStyles.AdjustToUniversal) == DateTimeStyles.AdjustToUniversal
                        || (_dateTimeStyles & DateTimeStyles.AssumeUniversal) == DateTimeStyles.AssumeUniversal)
                {
                        value = value.ToUniversalTime();
                }

                text = value.ToString(_dateTimeFormat ?? DefaultDateTimeFormat, Culture);

                writer.WriteStringValue(text);
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">The json serializer options.</param>
        /// <returns>The deserialized object.</returns>
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
                string? dateText = reader.GetString();

                if (string.IsNullOrEmpty(dateText) == false)
                {
                        if (!string.IsNullOrEmpty(_dateTimeFormat))
                        {
                                return DateTimeOffset.ParseExact(dateText, _dateTimeFormat, Culture, _dateTimeStyles);
                        }
                        else
                        {
                                return DateTimeOffset.Parse(dateText, Culture, _dateTimeStyles);
                        }
                }
                else
                {
                        return default(DateTimeOffset);
                }
        }


        public static readonly IsoDateTimeOffsetConverter Singleton = new IsoDateTimeOffsetConverter();
    }
}
#pragma warning restore CS8618
#pragma warning restore CS8601
#pragma warning restore CS8603
