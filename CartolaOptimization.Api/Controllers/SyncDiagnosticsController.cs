using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Exposes administrative endpoints used to inspect synchronization history and persisted failure diagnostics.
/// </summary>
[ApiController]
[Route("api/admin/sync")]
[Produces("application/json")]
public sealed class SyncDiagnosticsController(ISyncDiagnosticsService syncDiagnosticsService) : ControllerBase
{
    /// <summary>
    /// Returns recent synchronization executions, optionally filtered by source system or status.
    /// </summary>
    /// <param name="sourceSystem">Optional source system filter, such as Cartola or GatoMestre.</param>
    /// <param name="status">Optional status filter, such as Success, Partial or Failed.</param>
    /// <param name="take">Maximum number of executions returned. Limited to 200 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A descending list of synchronization executions.</returns>
    [HttpGet("executions")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SyncExecutionListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutions(
        [FromQuery] string? sourceSystem,
        [FromQuery] string? status,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await syncDiagnosticsService.GetSyncExecutionsAsync(sourceSystem, status, take, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the complete payload of a single synchronization execution.
    /// </summary>
    /// <param name="id">Identifier of the execution row.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The requested synchronization execution when found.</returns>
    [HttpGet("executions/{id:int}")]
    [ProducesResponseType(typeof(SyncExecutionDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecutionById(int id, CancellationToken cancellationToken = default)
    {
        var result = await syncDiagnosticsService.GetSyncExecutionByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Returns persisted failure diagnostics, optionally filtered by source system, correlation id, error code or severity.
    /// </summary>
    /// <param name="sourceSystem">Optional source system filter, such as Cartola or GatoMestre.</param>
    /// <param name="correlationId">Optional execution correlation id.</param>
    /// <param name="errorCode">Optional normalized business error code.</param>
    /// <param name="severity">Optional severity filter, such as Warning or Error.</param>
    /// <param name="take">Maximum number of logs returned. Limited to 200 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A descending list of failure logs.</returns>
    [HttpGet("failures")]
    [ProducesResponseType(typeof(IReadOnlyCollection<IntegrationFailureLogListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFailures(
        [FromQuery] string? sourceSystem,
        [FromQuery] string? correlationId,
        [FromQuery] string? errorCode,
        [FromQuery] string? severity,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await syncDiagnosticsService.GetFailureLogsAsync(sourceSystem, correlationId, errorCode, severity, take, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the complete payload of a single persisted failure log.
    /// </summary>
    /// <param name="id">Identifier of the failure log row.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The requested failure log when found.</returns>
    [HttpGet("failures/{id:int}")]
    [ProducesResponseType(typeof(IntegrationFailureLogDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFailureById(int id, CancellationToken cancellationToken = default)
    {
        var result = await syncDiagnosticsService.GetFailureLogByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
