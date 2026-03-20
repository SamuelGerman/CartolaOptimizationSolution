using System.Text.Json;

namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Translates low-level exceptions into normalized business error codes for diagnostics and reporting.
/// </summary>
public static class IntegrationErrorClassifier
{
    public static string Classify(Exception exception)
    {
        if (exception is ExternalApiException externalApiException)
        {
            if (IsAthleteDetailNotFound(externalApiException))
            {
                return IntegrationErrorCode.AthleteDetailNotFound;
            }

            if (IsRoundNotPublished(externalApiException))
            {
                return IntegrationErrorCode.RoundNotPublished;
            }

            if (exception.InnerException is JsonException ||
                exception.Message.Contains("JSON", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase))
            {
                return IntegrationErrorCode.ExternalJsonSchemaChanged;
            }

            if (externalApiException.ResponseStatusCode is null)
            {
                return IntegrationErrorCode.ExternalNetworkError;
            }

            return externalApiException.ResponseStatusCode is >= 400
                ? IntegrationErrorCode.ExternalHttpError
                : IntegrationErrorCode.ExternalApplicationError;
        }

        if (exception is DataCollectionIntegrityException integrityException)
        {
            if (integrityException.Message.Contains("could not be mapped to all required local references", StringComparison.OrdinalIgnoreCase))
            {
                return IntegrationErrorCode.AthleteMappingFallbackFailed;
            }

            if (integrityException.Message.Contains("requested round", StringComparison.OrdinalIgnoreCase) &&
                integrityException.Message.Contains("confrontation", StringComparison.OrdinalIgnoreCase))
            {
                return IntegrationErrorCode.ConfrontationRoundMismatch;
            }

            if (integrityException.Message.Contains("historically closed", StringComparison.OrdinalIgnoreCase) ||
                integrityException.Message.Contains("local baseline", StringComparison.OrdinalIgnoreCase))
            {
                return IntegrationErrorCode.IncompleteHistoricalBaseline;
            }
        }

        return IntegrationErrorCode.Unknown;
    }

    private static bool IsAthleteDetailNotFound(ExternalApiException exception)
    {
        return exception.RequestUrl.Contains("/api/v2/atletas/", StringComparison.OrdinalIgnoreCase) &&
               (exception.ResponsePayload.Contains("Atleta n", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("Atleta n", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRoundNotPublished(ExternalApiException exception)
    {
        return exception.RequestUrl.Contains("/api/v2/atletas/", StringComparison.OrdinalIgnoreCase) &&
               exception.RequestUrl.Contains("/rodadas/", StringComparison.OrdinalIgnoreCase) &&
               exception.ResponseStatusCode == 404 &&
               exception.ResponsePayload.Contains("encontrado", StringComparison.OrdinalIgnoreCase);
    }
}
