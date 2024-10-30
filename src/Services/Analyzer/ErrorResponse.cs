using System;
using System.Collections.Generic;
using System.Text.Json;

// Here is an example of an error response:
/*
    The input Text is invalid.
    Status: 400 (Bad Request)
    ErrorCode: InvalidArgument

    Content:
    {
        ""error"": {
            ""code"": ""InvalidArgument"",
            ""message"": ""The input Text is invalid.""
        }
    }

    headers:
    Cache-Control: no-store, proxy-revalidate, no-cache, max-age=0, private
    Transfer-Encoding: chunked
    Pragma: no-cache
    request-id: 7d639fdc-9c6e-4825-9346-05cc9752a047
    apim-request-id: REDACTED
    x-envoy-upstream-service-time: REDACTED
    Strict-Transport-Security: REDACTED
    X-Content-Type-Options: REDACTED
    x-ms-region: REDACTED
    Date: Tue, 08 Oct 2024 17:35:14 GMT
    Content-Type: application/json
*/

namespace SpeechEnabledTvClient.Services.Analyzer
{
    /// <summary>
    /// Represents an error response from the Azure CLU service.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Represents the content of the error response.
        /// </summary>
        public class Content
        {
            public Error error { get; set; } = new Error();
        }

        /// <summary>
        /// Represents the error details.
        /// </summary>
        public class Error
        {
            public string code { get; set; } = string.Empty;
            public string message { get; set; } = string.Empty;
        }

        public string exception { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string errorCode { get; set; } = string.Empty;
        public Content content { get; set; } = new Content();
        public Dictionary<string, string> headers { get; set; } = new Dictionary<string, string>();
    
        /// <summary>
        /// Deserializes an error response from an IO stream.
        /// </summary>
        /// <param name="response">The error response content stream.</param>
        /// <returns>The deserialized error response.</returns>
        public static ErrorResponse FromContentStream(System.IO.Stream? contentStream)
        {
            if (contentStream == null)
            {
                return new ErrorResponse();
            }
            return FromContentStream(new System.IO.StreamReader(contentStream).ReadToEnd());
        }

        /// <summary>
        /// Deserializes an error response from a string.
        /// </summary>
        /// <param name="response">The error response string.</param>
        /// <returns>The deserialized error response.</returns>
        public static ErrorResponse FromContentStream(string response)
        {
            // Split the response into lines
            var lines = response.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Create a new error response object
            var errorResponse = new ErrorResponse
            {
                headers = new Dictionary<string, string>()
            };

            // Parse the lines of the response
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Exception:"))
                {
                    errorResponse.exception = lines[i].Substring("Exception:".Length).Trim();
                }
                else if (lines[i].StartsWith("Status:"))
                {
                    errorResponse.status = lines[i].Substring("Status:".Length).Trim();
                }
                else if (lines[i].StartsWith("ErrorCode:"))
                {
                    errorResponse.errorCode = lines[i].Substring("ErrorCode:".Length).Trim();
                }
                else if (lines[i].StartsWith("Content:"))
                {
                    // Parse the json content of the response
                    i += 1;
                    var jsonLines = new List<string>();
                    for (int j = i; j < lines.Length; j++, i++)
                    {
                        // a blank line indicates the end of the json content
                        if (string.IsNullOrWhiteSpace(lines[j]))
                        {
                            break;
                        }
                        jsonLines.Add(lines[j]);
                    }
                    // Join the json lines into a single string and deserialize it
                    string jsonContent = string.Join("\n", jsonLines);
                    var deserializedContent = JsonSerializer.Deserialize<Content>(jsonContent);
                    if (deserializedContent != null)
                    {
                        errorResponse.content = deserializedContent;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to deserialize content: {jsonContent}");
                    }
                }
                else if (lines[i].Contains(":"))
                {
                    var headerParts = lines[i].Split(new[] { ':' }, 2);
                    if (headerParts.Length == 2)
                    {
                        errorResponse.headers[headerParts[0].Trim()] = headerParts[1].Trim();
                    }
                }
            }

            return errorResponse;
        }

    }
}