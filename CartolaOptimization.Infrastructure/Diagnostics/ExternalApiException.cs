namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Representa uma falha ocorrida ao chamar ou desserializar a resposta de uma API externa.
/// </summary>
public sealed class ExternalApiException : Exception
{
    public ExternalApiException(
        string sourceSystem,
        string errorLocation,
        string requestMethod,
        string requestUrl,
        string requestHeadersJson,
        string requestPayload,
        int? responseStatusCode,
        string? responseReasonPhrase,
        string responseHeadersJson,
        string responsePayload,
        string message,
        Exception? innerException = null) : base(message, innerException)
    {
        SourceSystem = sourceSystem;
        ErrorLocation = errorLocation;
        RequestMethod = requestMethod;
        RequestUrl = requestUrl;
        RequestHeadersJson = requestHeadersJson;
        RequestPayload = requestPayload;
        ResponseStatusCode = responseStatusCode;
        ResponseReasonPhrase = responseReasonPhrase ?? string.Empty;
        ResponseHeadersJson = responseHeadersJson;
        ResponsePayload = responsePayload;
    }

    public string SourceSystem { get; }

    public string ErrorLocation { get; }

    public string RequestMethod { get; }

    public string RequestUrl { get; }

    public string RequestHeadersJson { get; }

    public string RequestPayload { get; }

    public int? ResponseStatusCode { get; }

    public string ResponseReasonPhrase { get; }

    public string ResponseHeadersJson { get; }

    public string ResponsePayload { get; }
}
