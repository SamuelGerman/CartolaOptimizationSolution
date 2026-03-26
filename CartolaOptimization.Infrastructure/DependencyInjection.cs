using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Infrastructure.Diagnostics;
using CartolaOptimization.Infrastructure.Integrations.Cartola;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre;
using CartolaOptimization.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CartolaOptimization.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpClient<ICartolaApiClient, CartolaApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.cartola.globo.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CartolaOptimization/1.0");
        });

        services.AddHttpClient<IGatoMestreApiClient, GatoMestreApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.gatomestre.globo.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CartolaOptimization/1.0");
        });

        services.AddScoped<ICartolaSyncService, CartolaSyncService>();
        services.AddScoped<IGatoMestreSyncService, GatoMestreSyncService>();
        services.AddScoped<IAthleteProjectionService, AthleteProjectionService>();
        services.AddScoped<ISyncDiagnosticsService, SyncDiagnosticsService>();
        services.AddScoped<ITrainingDatasetPreparationService, TrainingDatasetPreparationService>();
        services.AddScoped<ITrainingDatasetDiagnosticsService, TrainingDatasetDiagnosticsService>();
        services.AddScoped<IEndOfRoundMaintenanceService, EndOfRoundMaintenanceService>();
        services.AddScoped<IIntegrationFailureLogService, IntegrationFailureLogService>();
        services.AddScoped<ISyncExecutionLogService, SyncExecutionLogService>();
        services.AddScoped<ICartolaLineupSubmissionService, CartolaLineupSubmissionService>();

        return services;
    }
}
