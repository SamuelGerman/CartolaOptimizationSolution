using System.Text.Json;
using CartolaOptimization.Infrastructure.Diagnostics;

namespace CartolaOptimization.Tests;

public sealed class IntegrationErrorClassifierTests
{
    [Fact]
    public void Classify_ReturnsAthleteDetailNotFound_WhenGatoMestrePayloadSaysAthleteWasNotFound()
    {
        var exception = new ExternalApiException(
            sourceSystem: "GatoMestre",
            errorLocation: "GatoMestreApiClient.GetRequiredAsync",
            requestMethod: "GET",
            requestUrl: "https://api.gatomestre.globo.com/api/v2/atletas/37457?a=true",
            requestHeadersJson: "{}",
            requestPayload: string.Empty,
            responseStatusCode: 200,
            responseReasonPhrase: "OK",
            responseHeadersJson: "{}",
            responsePayload: """{"error":true,"message":"Atleta não encontrado"}""",
            message: "The Gato Mestre API returned an application error for the athlete detail endpoint.");

        var result = IntegrationErrorClassifier.Classify(exception);

        Assert.Equal(IntegrationErrorCode.AthleteDetailNotFound, result);
    }

    [Fact]
    public void Classify_ReturnsExternalJsonSchemaChanged_WhenExternalApiExceptionWrapsJsonException()
    {
        var exception = new ExternalApiException(
            sourceSystem: "GatoMestre",
            errorLocation: "GatoMestreApiClient.GetRequiredAsync",
            requestMethod: "GET",
            requestUrl: "https://api.gatomestre.globo.com/api/v2/atletas/130843?a=true",
            requestHeadersJson: "{}",
            requestPayload: string.Empty,
            responseStatusCode: 200,
            responseReasonPhrase: "OK",
            responseHeadersJson: "{}",
            responsePayload: """{"rodadas":[{"medias":{"geral":{"total":{"num":null}}}}]}""",
            message: "The Gato Mestre API payload could not be deserialized.",
            innerException: new JsonException("Cannot get the value of a token type 'Null' as a number."));

        var result = IntegrationErrorClassifier.Classify(exception);

        Assert.Equal(IntegrationErrorCode.ExternalJsonSchemaChanged, result);
    }

    [Fact]
    public void Classify_ReturnsIncompleteHistoricalBaseline_WhenIntegrityErrorMentionsHistoricalClosure()
    {
        var exception = new DataCollectionIntegrityException(
            sourceSystem: "Cartola",
            stepName: "Validate requested closed round",
            errorLocation: "CartolaSyncService.SyncClosedRoundAsync",
            message: "Round '6' is not considered historically closed because the current Cartola round is '6'.");

        var result = IntegrationErrorClassifier.Classify(exception);

        Assert.Equal(IntegrationErrorCode.IncompleteHistoricalBaseline, result);
    }
}
