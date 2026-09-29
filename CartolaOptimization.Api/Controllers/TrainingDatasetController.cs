using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Application.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Expõe os principais endpoints administrativos usados para manter a base de treino atualizada e operar o modelo full-context.
/// </summary>
[ApiController]
[Route("api/admin/training")]
[Produces("application/json")]
public sealed class TrainingDatasetController(
    ITrainingDatasetDiagnosticsService trainingDatasetDiagnosticsService,
    IEndOfRoundMaintenanceService endOfRoundMaintenanceService,
    IFullContextAthleteScoreModelTrainingService fullContextAthleteScoreModelTrainingService,
    IFullContextAthleteScoreModelHyperparameterTuningService fullContextAthleteScoreModelHyperparameterTuningService,
    IPositionalAthleteScoreModelTrainingService positionalAthleteScoreModelTrainingService) : ControllerBase
{
    /// <summary>
    /// Retorna um recorte histórico do conjunto de dados pronto para exportação que inclui contexto de atleta, clube, adversário e partida.
    /// </summary>
    /// <param name="fromRound">Rodada inicial opcional da janela de exportação.</param>
    /// <param name="toRound">Rodada final opcional da janela de exportação.</param>
    /// <param name="take">Número máximo de linhas retornadas. Limitado a 5000 linhas.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Um recorte histórico do conjunto de dados com features contextuais completas e valores-alvo.</returns>
    [HttpGet("datasets/full-context")]
    [ProducesResponseType(typeof(TrainingFullContextDatasetResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFullContextDataset(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int take = 1000,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetDiagnosticsService.GetFullContextDatasetAsync(
            fromRound,
            toRound,
            take,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Exporta o recorte do conjunto de dados de treino full-context como Excel para que todas as features construídas possam ser inspecionadas intencionalmente fora da API.
    /// </summary>
    /// <param name="fromRound">Rodada inicial opcional da janela de exportação.</param>
    /// <param name="toRound">Rodada final opcional da janela de exportação.</param>
    /// <param name="take">Número máximo de linhas retornadas. Limitado a 5000 linhas.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Um arquivo Excel com as linhas do conjunto de dados de treino full-context.</returns>
    [HttpGet("datasets/full-context/xlsx")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> ExportFullContextDatasetXlsx(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int take = 1000,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetDiagnosticsService.GetFullContextDatasetAsync(
            fromRound,
            toRound,
            take,
            cancellationToken);

        return CreateExcelFileResult("full-context", result.Rows, fromRound, toRound);
    }

    /// <summary>
    /// Treina e persiste o principal modelo de aprendizado de máquina de pontuação de atletas full-context.
    /// A importância das features por permutação é sempre calculada após a passagem inicial de treino e armazenada nos metadados do modelo.
    /// Quando <paramref name="topNFeatures"/> é fornecido, o modelo é então retreinado usando apenas as top-N features numéricas,
    /// o que pode reduzir ruído e melhorar a generalização quando muitas features são redundantes.
    /// </summary>
    /// <param name="fromRound">Rodada inicial opcional da janela de treino.</param>
    /// <param name="toRound">Rodada final opcional da janela de treino.</param>
    /// <param name="validationRounds">Número de rodadas finais reservadas para validação temporal. Padrão de uma rodada.</param>
    /// <param name="topNFeatures">
    /// Quando especificado, retreina o modelo usando apenas as top-N features numéricas mais importantes (conforme ranqueadas por
    /// importância das features por permutação no conjunto de holdout). As features categóricas (clube, adversário, posição,
    /// status) são sempre incluídas. Omita este parâmetro para treinar em todas as features disponíveis.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Os metadados do modelo e as métricas de validação do artefato full-context recém-treinado.</returns>
    [HttpPost("models/athlete-score-v1-full-context/train")]
    [ProducesResponseType(typeof(AthleteScoreModelTrainingResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrainFullContextAthleteScoreModel(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int validationRounds = 1,
        [FromQuery] int? topNFeatures = null,
        CancellationToken cancellationToken = default)
    {
        var result = await fullContextAthleteScoreModelTrainingService.TrainAsync(
            fromRound,
            toRound,
            validationRounds,
            topNFeatures,
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Retorna os metadados sobre o artefato de modelo de pontuação de atletas full-context atualmente persistido, se houver.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Os metadados do modelo full-context ativo ou 404 quando o artefato ainda não foi treinado.</returns>
    [HttpGet("models/athlete-score-v1-full-context")]
    [ProducesResponseType(typeof(AthleteScoreModelInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullContextAthleteScoreModelInfo(CancellationToken cancellationToken = default)
    {
        var result = await fullContextAthleteScoreModelTrainingService.GetCurrentModelInfoAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Treina e persiste um modelo LightGBM de pontuação de atletas separado para cada posição do Cartola
    /// (GOL, LAT, ZAG, MEI, ATA, TEC) usando o mesmo conjunto de features full-context do modelo global.
    /// Posições com menos linhas que o limiar mínimo configurado são descartadas automaticamente.
    /// </summary>
    /// <param name="fromRound">Rodada inicial opcional da janela de treino.</param>
    /// <param name="toRound">Rodada final opcional da janela de treino.</param>
    /// <param name="validationRounds">Número de rodadas finais reservadas para validação temporal. Padrão de uma rodada.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Resultados de treino por posição, incluindo métricas do modelo e eventuais motivos de descarte.</returns>
    [HttpPost("models/athlete-score-v1-positional/train")]
    [ProducesResponseType(typeof(PositionalAthleteScoreTrainingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TrainPositionalAthleteScoreModels(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int validationRounds = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await positionalAthleteScoreModelTrainingService.TrainAllAsync(
            fromRound,
            toRound,
            validationRounds,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Retorna os metadados de todos os artefatos de modelo posicional de pontuação de atletas persistidos.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Uma lista de metadados de modelos ordenada por posição; vazia quando nenhum artefato posicional foi treinado ainda.</returns>
    [HttpGet("models/athlete-score-v1-positional")]
    [ProducesResponseType(typeof(IReadOnlyList<AthleteScoreModelInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPositionalAthleteScoreModelInfos(CancellationToken cancellationToken = default)
    {
        var result = await positionalAthleteScoreModelTrainingService.GetAllModelInfosAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Executa o fluxo de manutenção de fim de rodada: sincroniza o mercado atual, faz backfill apenas das rodadas históricas fechadas que faltam, atualiza os metadados de prontidão e retreina o modelo full-context principal quando necessário.
    /// </summary>
    /// <param name="validationRounds">Número de rodadas finais reservadas para validação temporal quando o modelo é retreinado. Padrão de uma rodada.</param>
    /// <param name="forceLatestClosedRound">Quando verdadeiro, força o reprocessamento da última rodada fechada mesmo que já tenha sido finalizada antes.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Um resumo consolidado do fluxo automatizado de manutenção de fim de rodada.</returns>
    [HttpPost("maintenance/end-of-round")]
    [ProducesResponseType(typeof(EndOfRoundMaintenanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RunEndOfRoundMaintenance(
        [FromQuery] int validationRounds = 1,
        [FromQuery] bool forceLatestClosedRound = false,
        CancellationToken cancellationToken = default)
    {
        var result = await endOfRoundMaintenanceService.RunAsync(
            validationRounds,
            forceLatestClosedRound,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Executa uma otimização de hiperparâmetros por busca aleatória para o modelo LightGBM full-context usando
    /// validação cruzada temporal walk-forward, e então opcionalmente retreina e persiste o modelo com
    /// a melhor combinação encontrada.
    /// </summary>
    /// <param name="fromRound">Rodada inicial opcional a incluir no conjunto de dados da busca.</param>
    /// <param name="toRound">Rodada final opcional a incluir no conjunto de dados da busca.</param>
    /// <param name="numberOfTrials">
    /// Número de combinações aleatórias de hiperparâmetros a avaliar. Mais trials aumentam a chance de
    /// de encontrar o ótimo global ao custo de um tempo de execução maior. Padrão de 30.
    /// </param>
    /// <param name="validationFolds">
    /// Número de folds de validação cruzada walk-forward. Cada fold reserva uma rodada adicional de
    /// a cauda do conjunto de dados. Padrão de 3.
    /// </param>
    /// <param name="applyBest">
    /// Quando <c>true</c> (padrão), retreina e persiste o modelo full-context com a melhor
    /// combinação de hiperparâmetros e a seleção de features <c>DefaultTopNFeatures</c> configurada.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Resultados da busca, incluindo as pontuações de CV de todos os trials, os melhores hiperparâmetros e os metadados do modelo final.</returns>
    [HttpPost("models/athlete-score-v1-full-context/hyperparameter-search")]
    [ProducesResponseType(typeof(HyperparameterSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SearchFullContextHyperparameters(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int numberOfTrials = 30,
        [FromQuery] int validationFolds = 3,
        [FromQuery] bool applyBest = true,
        CancellationToken cancellationToken = default)
    {
        var result = await fullContextAthleteScoreModelHyperparameterTuningService.SearchAsync(
            fromRound,
            toRound,
            numberOfTrials,
            validationFolds,
            applyBest,
            cancellationToken);

        return Ok(result);
    }

    private FileContentResult CreateExcelFileResult<T>(string datasetKey, IReadOnlyCollection<T> rows, int? fromRound, int? toRound)
    {
        var content = TrainingDatasetExcelExporter.ExportXlsx(datasetKey, rows);
        var fileName = BuildDatasetFileName(datasetKey, fromRound, toRound);
        return File(content, TrainingDatasetExcelExporter.ContentType, fileName);
    }

    private static string BuildDatasetFileName(string datasetKey, int? fromRound, int? toRound)
    {
        var windowLabel = fromRound.HasValue || toRound.HasValue
            ? $"r{fromRound?.ToString() ?? "start"}-r{toRound?.ToString() ?? "end"}"
            : "all-rounds";

        return $"training-{datasetKey}-{windowLabel}.xlsx";
    }
}
