using CartolaOptimization.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

[ApiController]
[Route("api/cartola/sync")]
public sealed class CartolaSyncController(ICartolaSyncService cartolaSyncService) : ControllerBase
{
    [HttpPost("current")]
    public async Task<IActionResult> SyncCurrent(CancellationToken cancellationToken)
    {
        var result = await cartolaSyncService.SyncAsync(cancellationToken: cancellationToken);
        return Ok(result);
    }

    [HttpPost("matches/{round:int}")]
    public async Task<IActionResult> SyncMatchesRound(int round, CancellationToken cancellationToken)
    {
        var result = await cartolaSyncService.SyncAsync(round, cancellationToken);
        return Ok(result);
    }
}
