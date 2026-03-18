namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Defines normalized business error codes used by synchronization diagnostics.
/// </summary>
public static class IntegrationErrorCode
{
    public const string Unknown = "Unknown";
    public const string ExternalNetworkError = "ExternalNetworkError";
    public const string ExternalHttpError = "ExternalHttpError";
    public const string ExternalApplicationError = "ExternalApplicationError";
    public const string ExternalJsonSchemaChanged = "ExternalJsonSchemaChanged";
    public const string AthleteDetailNotFound = "AthleteDetailNotFound";
    public const string AthleteMappingFallbackFailed = "AthleteMappingFallbackFailed";
    public const string ConfrontationRoundMismatch = "ConfrontationRoundMismatch";
}
