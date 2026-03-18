using CartolaOptimization.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Middleware;

/// <summary>
/// Converts synchronization failures into structured problem details responses.
/// </summary>
public sealed class ApiExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DataCollectionProcessException exception)
        {
            logger.LogError(
                exception,
                "Data collection process failure for {SourceSystem} at step {StepName}. CorrelationId: {CorrelationId}",
                exception.SourceSystem,
                exception.StepName,
                exception.CorrelationId);

            await WriteProblemDetailsAsync(
                context,
                title: "Data collection failed",
                detail: exception.Message,
                extensions: new Dictionary<string, object?>
                {
                    ["sourceSystem"] = exception.SourceSystem,
                    ["processName"] = exception.ProcessName,
                    ["stepName"] = exception.StepName,
                    ["correlationId"] = exception.CorrelationId,
                    ["errorLogId"] = exception.ErrorLogId,
                });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected server error while processing {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await WriteProblemDetailsAsync(
                context,
                title: "Unexpected server error",
                detail: "An unexpected error occurred while processing the request.",
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = context.TraceIdentifier,
                });
        }
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        string title,
        string detail,
        IReadOnlyDictionary<string, object?> extensions)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path,
        };

        foreach (var extension in extensions)
        {
            problemDetails.Extensions[extension.Key] = extension.Value;
        }

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
