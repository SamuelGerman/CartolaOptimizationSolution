using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;

namespace CartolaOptimization.Infrastructure.Services;

/// <summary>
/// Centraliza as regras que classificam as linhas de rodada como completas, parciais ou inaptas para o treino do modelo.
/// </summary>
internal static class TrainingReadinessEvaluator
{
    public static void ApplyCartolaBaseline(AthleteRound athleteRound, int currentRound, bool hasCompleteClubRoundFeatures)
    {
        athleteRound.DataPhase = athleteRound.RoundId < currentRound
            ? RoundDataPhaseValues.HistoricalClosed
            : RoundDataPhaseValues.CurrentOpen;

        ApplyAthleteReadiness(athleteRound, hasCompleteClubRoundFeatures);
    }

    public static void ApplyAthleteEnrichment(AthleteRound athleteRound, bool hasCompleteClubRoundFeatures)
    {
        if (string.IsNullOrWhiteSpace(athleteRound.DataPhase))
        {
            athleteRound.DataPhase = RoundDataPhaseValues.Unknown;
        }

        ApplyAthleteReadiness(athleteRound, hasCompleteClubRoundFeatures);
    }

    public static void ApplyClubRoundReadiness(ClubRound clubRound)
    {
        var hasPositionStats = clubRound.PositionStats is { Count: > 0 };

        clubRound.IsTrainingEligible = hasPositionStats;
        clubRound.TrainingReadinessStatus = hasPositionStats
            ? TrainingReadinessStatusValues.Complete
            : TrainingReadinessStatusValues.Partial;
        clubRound.TrainingReadinessReason = hasPositionStats
            ? string.Empty
            : "Club round was synchronized without position statistics, so the enrichment is incomplete for model training.";
    }

    public static void ApplyAthleteReadiness(AthleteRound athleteRound, bool hasCompleteClubRoundFeatures)
    {
        if (athleteRound.ClubId == 0 || athleteRound.PositionId == 0 || athleteRound.StatusId == 0)
        {
            athleteRound.IsTrainingEligible = false;
            athleteRound.TrainingReadinessStatus = TrainingReadinessStatusValues.UnfitForTraining;
            athleteRound.TrainingReadinessReason = "The athlete round is missing required structural references such as club, position or status.";
            return;
        }

        if (string.Equals(athleteRound.DataPhase, RoundDataPhaseValues.CurrentOpen, StringComparison.OrdinalIgnoreCase))
        {
            athleteRound.IsTrainingEligible = false;
            athleteRound.TrainingReadinessStatus = TrainingReadinessStatusValues.UnfitForTraining;
            athleteRound.TrainingReadinessReason = "The round is still open or current, so its final target value is not considered stable for training.";
            return;
        }

        if (!hasCompleteClubRoundFeatures)
        {
            athleteRound.IsTrainingEligible = false;
            athleteRound.TrainingReadinessStatus = TrainingReadinessStatusValues.Partial;
            athleteRound.TrainingReadinessReason = string.Equals(athleteRound.DataPhase, RoundDataPhaseValues.Unknown, StringComparison.OrdinalIgnoreCase)
                ? "The row is enriched, but its phase is still unknown because the Cartola baseline has not confirmed whether the round is historically closed."
                : "The athlete row is available, but club-level Gato Mestre features for the same round are still missing or incomplete.";
            return;
        }

        if (string.Equals(athleteRound.DataPhase, RoundDataPhaseValues.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            athleteRound.IsTrainingEligible = false;
            athleteRound.TrainingReadinessStatus = TrainingReadinessStatusValues.Partial;
            athleteRound.TrainingReadinessReason = "The row has club-level enrichment, but the round phase is still unknown until the Cartola baseline confirms a closed historical round.";
            return;
        }

        athleteRound.IsTrainingEligible = true;
        athleteRound.TrainingReadinessStatus = TrainingReadinessStatusValues.Complete;
        athleteRound.TrainingReadinessReason = string.Empty;
    }
}
