namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Define códigos de erro de negócio normalizados usados pelos diagnósticos de sincronização.
/// </summary>
public static class IntegrationErrorCode
{
    public const string Unknown = "Unknown";
    public const string ExternalNetworkError = "ExternalNetworkError";
    public const string ExternalHttpError = "ExternalHttpError";
    public const string ExternalApplicationError = "ExternalApplicationError";
    public const string ExternalJsonSchemaChanged = "ExternalJsonSchemaChanged";
    public const string RoundNotPublished = "RoundNotPublished";
    public const string AthleteDetailNotFound = "AthleteDetailNotFound";
    public const string AthleteMappingFallbackFailed = "AthleteMappingFallbackFailed";
    public const string ConfrontationRoundMismatch = "ConfrontationRoundMismatch";
    public const string IncompleteHistoricalBaseline = "IncompleteHistoricalBaseline";
}
