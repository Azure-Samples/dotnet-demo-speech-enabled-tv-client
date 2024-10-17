namespace SpeechEnabledTvClient .Services.Analyzer
{
    /// <summary>
    /// Represents the response from the analyzer.
    /// </summary>
    public class AnalyzerResponse
    {
        public readonly bool IsError = false;
        public readonly bool HasErrorResponse = false;
        public readonly Interpretation interpretation = new Interpretation();
        public readonly ErrorResponse error = new ErrorResponse();

        /// <summary>
        /// Initializes an empty instance of the <see cref="AnalyzerResponse"/> class.
        /// </summary>
        public AnalyzerResponse() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AnalyzerResponse"/> class with a successful <see cref="Interpretation"/>.
        /// </summary>
        public AnalyzerResponse(Interpretation interpretation)
        {
            this.interpretation = interpretation;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AnalyzerResponse"/> class with an <see cref="ErrorResponse"/>.
        /// </summary>
        public AnalyzerResponse(ErrorResponse error)
        {
            this.error = error;
            this.IsError = true;
            this.HasErrorResponse = true;
        }
    }
}