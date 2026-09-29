namespace CartolaOptimization.Domain.Constants;

/// <summary>
/// Define os valores de prontidão usados para indicar se uma linha pode ser usada pelo conjunto de dados de treino.
/// </summary>
public static class TrainingReadinessStatusValues
{
    public const string Complete = "Complete";
    public const string Partial = "Partial";
    public const string UnfitForTraining = "UnfitForTraining";
}
