namespace CartolaOptimization.Domain.Entities
{
    /// <summary>
    /// Persists detailed diagnostics for failures that occur while collecting or synchronizing external datasets.
    /// </summary>
    public sealed class IntegrationFailureLog
    {
        /// <summary>
        /// Gets or sets the primary identifier of the failure log row.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp in which the failure was captured.
        /// </summary>
        public DateTime OccurredAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the correlation id used to group all steps of a synchronization execution.
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the external source involved in the failure, such as Cartola or GatoMestre.
        /// </summary>
        public string SourceSystem { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process name that was running when the failure happened.
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the pipeline step that was executing when the failure happened.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the method or component where the failure was detected.
        /// </summary>
        public string ErrorLocation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTTP method used for the external request.
        /// </summary>
        public string RequestMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute request URL used when calling the external API.
        /// </summary>
        public string RequestUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized request headers.
        /// </summary>
        public string RequestHeadersJson { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized request payload sent to the external API.
        /// </summary>
        public string RequestPayload { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTTP status code returned by the external API when available.
        /// </summary>
        public int? ResponseStatusCode { get; set; }

        /// <summary>
        /// Gets or sets the HTTP reason phrase returned by the external API when available.
        /// </summary>
        public string ResponseReasonPhrase { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized response headers.
        /// </summary>
        public string ResponseHeadersJson { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the raw response payload received from the external API.
        /// </summary>
        public string ResponsePayload { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized list of steps executed before the failure.
        /// </summary>
        public string ExecutedStepsJson { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized business context associated with the failure.
        /// </summary>
        public string ContextJson { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the exception raised by the process.
        /// </summary>
        public string ExceptionType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exception message.
        /// </summary>
        public string ExceptionMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the message of the inner exception when available.
        /// </summary>
        public string InnerExceptionMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the captured exception stack trace.
        /// </summary>
        public string StackTrace { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the main synchronization transaction was rolled back.
        /// </summary>
        public bool TransactionRolledBack { get; set; }
    }
}
