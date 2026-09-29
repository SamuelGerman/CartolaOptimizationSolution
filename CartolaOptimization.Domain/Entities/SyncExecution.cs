namespace CartolaOptimization.Domain.Entities;

/// <summary>
/// Persiste o ciclo de vida e as métricas de qualidade de cada execução de sincronização.
/// </summary>
public sealed class SyncExecution
{
    /// <summary>
    /// Obtém ou define o identificador primário da linha de execução.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Obtém ou define o id de correlação compartilhado com os logs de falha emitidos pela mesma execução.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define o sistema de origem sincronizado, como Cartola ou GatoMestre.
    /// </summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define o nome do processo responsável pela sincronização.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define o timestamp UTC em que a execução começou.
    /// </summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// Obtém ou define o timestamp UTC em que a execução terminou.
    /// </summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>
    /// Obtém ou define a duração total da execução em milissegundos quando disponível.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Obtém ou define o status final da execução.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define o passo mais recente do pipeline alcançado pela execução.
    /// </summary>
    public string LastStepName { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define a temporada solicitada quando a execução tem como alvo um conjunto de dados sazonal.
    /// </summary>
    public int? Season { get; set; }

    /// <summary>
    /// Obtém ou define a rodada solicitada quando a execução tem como alvo um conjunto de dados específico de rodada.
    /// </summary>
    public int? Round { get; set; }

    /// <summary>
    /// Obtém ou define o código de erro de negócio normalizado associado à execução quando aplicável.
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define a nota final da execução mostrada aos operadores.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define as métricas serializadas produzidas pela execução.
    /// </summary>
    public string MetricsJson { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define o contexto de negócio serializado capturado para a execução.
    /// </summary>
    public string ContextJson { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define os limiares de qualidade serializados usados para avaliar a execução.
    /// </summary>
    public string QualityCriteriaJson { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define a taxa de cobertura alcançada quando a métrica se aplica.
    /// </summary>
    public decimal? CoverageRate { get; set; }

    /// <summary>
    /// Obtém ou define o percentual de itens descartados quando a métrica se aplica.
    /// </summary>
    public decimal? SkippedRate { get; set; }

    /// <summary>
    /// Obtém ou define um valor que indica se a execução atingiu os critérios de qualidade configurados.
    /// </summary>
    public bool? QualityCriteriaSatisfied { get; set; }

    /// <summary>
    /// Obtém ou define o número total de logs de aviso persistidos vinculados à execução.
    /// </summary>
    public int WarningLogsCount { get; set; }

    /// <summary>
    /// Obtém ou define o número total de logs de erro persistidos vinculados à execução.
    /// </summary>
    public int ErrorLogsCount { get; set; }
}
