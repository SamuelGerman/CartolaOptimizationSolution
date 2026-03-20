using CartolaOptimization.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CartolaOptimization.ML;

public static class DependencyInjection
{
    public static IServiceCollection AddMachineLearningServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AthleteScoreModelOptions>(configuration.GetSection("MachineLearning"));
        services.Configure<FullContextAthleteScoreModelOptions>(configuration.GetSection("MachineLearning:FullContext"));
        services.AddSingleton<AthleteScoreModelArtifactStore>();
        services.AddSingleton<FullContextAthleteScoreModelArtifactStore>();
        services.AddScoped<IAthleteScoreModelTrainingService, AthleteScoreModelTrainingService>();
        services.AddScoped<IAthleteScorePredictionService, AthleteScorePredictionService>();
        services.AddScoped<IFullContextAthleteScoreModelTrainingService, FullContextAthleteScoreModelTrainingService>();
        services.AddScoped<IFullContextAthleteScorePredictionService, FullContextAthleteScorePredictionService>();
        services.AddScoped<IPredictedLineupOptimizationService, PredictedLineupOptimizationService>();
        return services;
    }
}
