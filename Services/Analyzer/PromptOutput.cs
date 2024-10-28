namespace SpeechEnabledTvClient.Services.Analyzer
{
    /// <summary>
    /// Represents the output of a prompt completion.
    /// </summary>
    public class PromptOutput
    {
        public ResponseData ResponseData { get; set; } = new ResponseData();
        public bool IsError { get; set; }
        public int StatusCode { get; set; }
        public string ReasonPhrase { get; set; } = string.Empty;
    }
}