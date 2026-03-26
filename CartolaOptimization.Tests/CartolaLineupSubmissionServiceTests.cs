using System.Text.Json;
using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Infrastructure.Services;

namespace CartolaOptimization.Tests;

public sealed class CartolaLineupSubmissionServiceTests
{
    [Fact]
    public async Task SubmitAsync_BuildsCartolaPayloadWithAutoCaptainAndReserves_WhenSubmissionIsRequested()
    {
        var cartolaApiClient = new FakeCartolaApiClient
        {
            OnSaveTeamAsync = (_, _, _, _) =>
            {
                using var document = JsonDocument.Parse("{\"ok\":true}");
                return Task.FromResult(document.RootElement.Clone());
            },
        };

        var predictionPool = new AthleteScorePredictionResult
        {
            DatasetKey = "athlete-context",
            Athletes =
            [
                CreatePredictionItem(110708, 1, 3.15m, 5.78m),
                CreatePredictionItem(78445, 2, 5.11m, 10.43m),
                CreatePredictionItem(107172, 2, 3.35m, 6.18m),
                CreatePredictionItem(95131, 3, 4.07m, 9.66m),
                CreatePredictionItem(97878, 3, 2.52m, 4.79m),
                CreatePredictionItem(89837, 4, 8.42m, 12.77m),
                CreatePredictionItem(98384, 4, 3.33m, 7.36m),
                CreatePredictionItem(39656, 4, 2.61m, 5.46m),
                CreatePredictionItem(98873, 5, 4.90m, 10.91m),
                CreatePredictionItem(131516, 5, 4.87m, 10.44m),
                CreatePredictionItem(97867, 5, 3.89m, 8.84m),
                CreatePredictionItem(126977, 6, 0.92m, 7.23m),
                CreatePredictionItem(210001, 1, 2.15m, 4.10m),
                CreatePredictionItem(210002, 2, 4.20m, 7.10m),
                CreatePredictionItem(210003, 3, 3.90m, 6.70m),
                CreatePredictionItem(210004, 4, 5.40m, 8.30m),
                CreatePredictionItem(210005, 5, 4.70m, 9.10m),
            ],
        };

        var service = new CartolaLineupSubmissionService(
            cartolaApiClient,
            new StubAthleteScorePredictionService(new AthleteScorePredictionResult()),
            new StubAthleteContextPredictionService(predictionPool),
            new FakeFullContextAthleteScorePredictionService());

        var lineup = new PredictedLineupResult
        {
            Round = 9,
            DatasetKey = "athlete-context",
            Formation =
            [
                new PredictedLineupPositionRequirement { PositionCartolaId = 1, Quantity = 1 },
                new PredictedLineupPositionRequirement { PositionCartolaId = 2, Quantity = 2 },
                new PredictedLineupPositionRequirement { PositionCartolaId = 3, Quantity = 2 },
                new PredictedLineupPositionRequirement { PositionCartolaId = 4, Quantity = 3 },
                new PredictedLineupPositionRequirement { PositionCartolaId = 5, Quantity = 3 },
                new PredictedLineupPositionRequirement { PositionCartolaId = 6, Quantity = 1 },
            ],
            Athletes =
            [
                CreateLineupItem(110708, 1, 3.15m, 5.78m),
                CreateLineupItem(78445, 2, 5.11m, 10.43m),
                CreateLineupItem(107172, 2, 3.35m, 6.18m),
                CreateLineupItem(95131, 3, 4.07m, 9.66m),
                CreateLineupItem(97878, 3, 2.52m, 4.79m),
                CreateLineupItem(89837, 4, 8.42m, 12.77m),
                CreateLineupItem(98384, 4, 3.33m, 7.36m),
                CreateLineupItem(39656, 4, 2.61m, 5.46m),
                CreateLineupItem(98873, 5, 4.90m, 10.91m),
                CreateLineupItem(131516, 5, 4.87m, 10.44m),
                CreateLineupItem(97867, 5, 3.89m, 8.84m),
                CreateLineupItem(126977, 6, 0.92m, 7.23m),
            ],
        };

        var request = new PredictedLineupRequest
        {
            Budget = 100m,
            CartolaSubmission = new CartolaLineupSubmissionRequest
            {
                SaveToCartola = true,
            },
        };

        var result = await service.SubmitAsync(
            round: 9,
            lineup,
            request,
            cartolaAuthorization: "Bearer test-token",
            cartolaGlbTag: "1502374855");

        var saveCall = Assert.Single(cartolaApiClient.SaveTeamCalls);
        Assert.Equal("Bearer test-token", saveCall.Authorization);
        Assert.Equal("1502374855", saveCall.GlbTag);
        Assert.Equal(3, saveCall.Request.Esquema);
        Assert.Equal(89837, saveCall.Request.Capitao);
        Assert.Equal(12, saveCall.Request.Atletas.Count);
        Assert.NotNull(saveCall.Request.Reservas);
        Assert.Equal(5, saveCall.Request.Reservas!.Count);
        Assert.Equal(210001, saveCall.Request.Reservas[1]);
        Assert.Equal(210002, saveCall.Request.Reservas[2]);
        Assert.Equal(210003, saveCall.Request.Reservas[3]);
        Assert.Equal(210004, saveCall.Request.Reservas[4]);
        Assert.Equal(210005, saveCall.Request.Reservas[5]);
        Assert.Equal(210004, saveCall.Request.ReservaLuxoId);

        Assert.True(result.Submitted);
        Assert.Equal(3, result.SchemeId);
        Assert.Equal(89837, result.CaptainAthleteCartolaId);
        Assert.Equal(210004, result.ReserveLuxuryAthleteCartolaId);
        Assert.Equal(5, result.ReserveSelections.Count);
        Assert.Equal(JsonValueKind.Object, result.CartolaResponse?.ValueKind);
    }

    [Fact]
    public async Task SubmitAsync_Throws_WhenAuthorizationHeaderIsMissing()
    {
        var service = new CartolaLineupSubmissionService(
            new FakeCartolaApiClient(),
            new StubAthleteScorePredictionService(new AthleteScorePredictionResult()),
            new StubAthleteContextPredictionService(new AthleteScorePredictionResult()),
            new FakeFullContextAthleteScorePredictionService());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(
            round: 9,
            lineup: new PredictedLineupResult
            {
                DatasetKey = "athlete-context",
                Formation =
                [
                    new PredictedLineupPositionRequirement { PositionCartolaId = 1, Quantity = 1 },
                    new PredictedLineupPositionRequirement { PositionCartolaId = 2, Quantity = 2 },
                    new PredictedLineupPositionRequirement { PositionCartolaId = 3, Quantity = 2 },
                    new PredictedLineupPositionRequirement { PositionCartolaId = 4, Quantity = 3 },
                    new PredictedLineupPositionRequirement { PositionCartolaId = 5, Quantity = 3 },
                    new PredictedLineupPositionRequirement { PositionCartolaId = 6, Quantity = 1 },
                ],
                Athletes = [CreateLineupItem(1, 1, 1m, 1m)],
            },
            request: new PredictedLineupRequest
            {
                Budget = 100m,
                CartolaSubmission = new CartolaLineupSubmissionRequest
                {
                    SaveToCartola = true,
                },
            },
            cartolaAuthorization: null));

        Assert.Contains("X-Cartola-Authorization", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AthleteScorePredictionItem CreatePredictionItem(
        int athleteCartolaId,
        int positionCartolaId,
        decimal predictedScore,
        decimal priceNum)
        => new()
        {
            AthleteCartolaId = athleteCartolaId,
            AthleteName = $"Athlete {athleteCartolaId}",
            AthleteNickname = $"Athlete {athleteCartolaId}",
            ClubCartolaId = 100 + positionCartolaId,
            ClubName = $"Club {positionCartolaId}",
            PositionCartolaId = positionCartolaId,
            PositionName = $"Position {positionCartolaId}",
            StatusCartolaId = 7,
            StatusName = "Provavel",
            BaselineRound = 9,
            BaselineDataPhase = "CurrentOpen",
            PriceNum = priceNum,
            MovingAverage = predictedScore,
            AverageScore = predictedScore,
            PredictedScore = predictedScore,
        };

    private static PredictedLineupAthleteItem CreateLineupItem(
        int athleteCartolaId,
        int positionCartolaId,
        decimal predictedScore,
        decimal priceNum)
        => new()
        {
            AthleteCartolaId = athleteCartolaId,
            AthleteName = $"Athlete {athleteCartolaId}",
            AthleteNickname = $"Athlete {athleteCartolaId}",
            ClubCartolaId = 100 + positionCartolaId,
            ClubName = $"Club {positionCartolaId}",
            PositionCartolaId = positionCartolaId,
            PositionName = $"Position {positionCartolaId}",
            StatusCartolaId = 7,
            StatusName = "Provavel",
            BaselineRound = 9,
            BaselineDataPhase = "CurrentOpen",
            PriceNum = priceNum,
            PredictedScore = predictedScore,
        };

    private sealed class StubAthleteScorePredictionService(AthleteScorePredictionResult result) : IAthleteScorePredictionService
    {
        public Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
            int round,
            int? statusCartolaId = null,
            int? clubCartolaId = null,
            int? positionCartolaId = null,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class StubAthleteContextPredictionService(AthleteScorePredictionResult result) : IAthleteContextAthleteScorePredictionService
    {
        public Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
            int round,
            int? statusCartolaId = null,
            int? clubCartolaId = null,
            int? positionCartolaId = null,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
