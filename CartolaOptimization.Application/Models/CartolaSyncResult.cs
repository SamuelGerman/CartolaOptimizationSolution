namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa o resultado de uma execução de sincronização do Cartola.
/// </summary>
public sealed class CartolaSyncResult
{
    /// <summary>
    /// Obtém o identificador persistido da execução da sincronização.
    /// </summary>
    public int? SyncExecutionId { get; init; }

    /// <summary>
    /// Obtém o id de correlação compartilhado com os diagnósticos emitidos durante a execução.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Obtém o status final da execução da sincronização.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Obtém o timestamp UTC em que a sincronização começou.
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Obtém o timestamp UTC em que a sincronização terminou.
    /// </summary>
    public DateTime FinishedAtUtc { get; init; }

    /// <summary>
    /// Obtém a duração total da sincronização em milissegundos.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Obtém um valor que indica se a execução satisfez seus critérios de qualidade.
    /// </summary>
    public bool QualityCriteriaSatisfied { get; init; }

    /// <summary>
    /// Obtém os critérios de qualidade legíveis aplicados à execução.
    /// </summary>
    public string QualityCriteriaDescription { get; init; } = string.Empty;

    /// <summary>
    /// Obtém a temporada informada pelo endpoint de status do mercado do Cartola.
    /// </summary>
    public int Season { get; init; }

    /// <summary>
    /// Obtém a rodada atual informada pelo endpoint de status do mercado do Cartola.
    /// </summary>
    public int CurrentRound { get; init; }

    /// <summary>
    /// Obtém a rodada usada para sincronizar as partidas.
    /// </summary>
    public int MatchesRound { get; init; }

    /// <summary>
    /// Obtém as atleta-rodadas afetadas pela sincronização.
    /// </summary>
    public IReadOnlyCollection<int> AthleteRounds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Obtém o número total de registros de clube disponíveis após a etapa de upsert.
    /// </summary>
    public int ClubsUpserted { get; init; }

    /// <summary>
    /// Obtém o número total de registros de posição disponíveis após a etapa de upsert.
    /// </summary>
    public int PositionsUpserted { get; init; }

    /// <summary>
    /// Obtém o número total de registros de status de atleta disponíveis após a etapa de upsert.
    /// </summary>
    public int StatusesUpserted { get; init; }

    /// <summary>
    /// Obtém o número total de registros de atleta disponíveis após a etapa de upsert.
    /// </summary>
    public int AthletesUpserted { get; init; }

    /// <summary>
    /// Obtém o número de linhas de partida processadas na sincronização.
    /// </summary>
    public int MatchesUpserted { get; init; }

    /// <summary>
    /// Obtém o número de linhas de atleta-rodada processadas na sincronização.
    /// </summary>
    public int AthleteRoundsUpserted { get; init; }

    /// <summary>
    /// Obtém o número de linhas de scout recriadas para as atleta-rodadas sincronizadas.
    /// </summary>
    public int ScoutsUpserted { get; init; }

    /// <summary>
    /// Obtém o número de atleta-rodadas locais existentes que foram finalizadas com zero pontos porque o payload fechado do Cartola não as continha explicitamente.
    /// </summary>
    public int AthleteRoundsMissingScorePayload { get; init; }

    /// <summary>
    /// Obtém o número de linhas do payload de pontuação final que não puderam ser mapeadas para uma linha-base local de atleta-rodada.
    /// </summary>
    public int ScorePayloadRowsWithoutLocalRound { get; init; }

    /// <summary>
    /// Obtém notas adicionais da execução que ajudam a interpretar sincronizações parciais ou históricas.
    /// </summary>
    public string Notes { get; init; } = string.Empty;
}
