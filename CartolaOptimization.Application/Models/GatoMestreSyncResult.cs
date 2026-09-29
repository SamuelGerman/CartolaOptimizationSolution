namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa o resultado de uma execução de sincronização do Gato Mestre.
/// </summary>
public sealed class GatoMestreSyncResult
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
    /// Obtém a temporada sincronizada.
    /// </summary>
    public int Season { get; init; }

    /// <summary>
    /// Obtém a rodada sincronizada.
    /// </summary>
    public int Round { get; init; }

    /// <summary>
    /// Obtém o número total de clubes disponíveis após a etapa de upsert do catálogo de times.
    /// </summary>
    public int ClubsUpserted { get; init; }

    /// <summary>
    /// Obtém o número de linhas de clube-rodada processadas para a rodada sincronizada.
    /// </summary>
    public int ClubRoundsUpserted { get; init; }

    /// <summary>
    /// Obtém o número de linhas de clube-rodada-posição processadas para a rodada sincronizada.
    /// </summary>
    public int ClubRoundPositionsUpserted { get; init; }

    /// <summary>
    /// Obtém o número de linhas de atleta-rodada enriquecidas ou criadas para a rodada sincronizada.
    /// </summary>
    public int AthleteRoundsUpserted { get; init; }

    /// <summary>
    /// Obtém o número de linhas de atleta descartadas porque os payloads disponíveis não foram suficientes para montar um registro de rodada confiável.
    /// </summary>
    public int AthleteRoundsSkipped { get; init; }

    /// <summary>
    /// Obtém o número de requisições de detalhe de atleta necessárias para completar os dados de rodada faltantes.
    /// </summary>
    public int AthleteDetailsFetched { get; init; }

    /// <summary>
    /// Obtém o número de logs de aviso persistidos ao tratar problemas não fatais de enriquecimento de atletas.
    /// </summary>
    public int AthleteFailuresLogged { get; init; }

    /// <summary>
    /// Obtém o percentual de cobertura de atletas alcançado durante a sincronização.
    /// </summary>
    public decimal AthleteCoverageRate { get; init; }

    /// <summary>
    /// Obtém o percentual de atletas descartados alcançado durante a sincronização.
    /// </summary>
    public decimal AthleteSkippedRate { get; init; }

    /// <summary>
    /// Obtém a taxa mínima de cobertura exigida para que a execução seja considerada saudável.
    /// </summary>
    public decimal MinimumCoverageRate { get; init; }

    /// <summary>
    /// Obtém o percentual máximo de atletas descartados permitido para que a execução seja considerada saudável.
    /// </summary>
    public decimal MaximumSkippedRate { get; init; }

    /// <summary>
    /// Obtém um valor que indica se a execução satisfez seus critérios de qualidade.
    /// </summary>
    public bool QualityCriteriaSatisfied { get; init; }

    /// <summary>
    /// Obtém os critérios de qualidade legíveis aplicados à execução.
    /// </summary>
    public string QualityCriteriaDescription { get; init; } = string.Empty;

    /// <summary>
    /// Obtém um valor que indica se os dados de confronto do time estavam disponíveis para a rodada solicitada.
    /// </summary>
    public bool ClubRoundDataAvailable { get; init; }

    /// <summary>
    /// Obtém um valor que indica se a sincronização terminou com enriquecimento parcial.
    /// </summary>
    public bool IsPartialSync { get; init; }

    /// <summary>
    /// Obtém uma nota opcional descrevendo detalhes relevantes da execução, como limitações do endpoint para dados históricos de confronto.
    /// </summary>
    public string Notes { get; init; } = string.Empty;
}
