using CartolaOptimization.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CartolaOptimization.ML;

public static class DependencyInjection
{
    public static IServiceCollection AddMachineLearningServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AthleteScoreModelOptions>(configuration.GetSection("MachineLearning"));
        services.Configure<AthleteContextAthleteScoreModelOptions>(configuration.GetSection("MachineLearning:AthleteContext"));
        services.Configure<FullContextAthleteScoreModelOptions>(configuration.GetSection("MachineLearning:FullContext"));
        services.AddSingleton<AthleteScoreModelArtifactStore>();
        services.AddSingleton<AthleteContextAthleteScoreModelArtifactStore>();
        services.AddSingleton<FullContextAthleteScoreModelArtifactStore>();
        services.AddScoped<IAthleteScoreModelTrainingService, AthleteScoreModelTrainingService>();
        services.AddScoped<IAthleteScorePredictionService, AthleteScorePredictionService>();
        services.AddScoped<IAthleteContextAthleteScoreModelTrainingService, AthleteContextAthleteScoreModelTrainingService>();
        services.AddScoped<IAthleteContextAthleteScorePredictionService, AthleteContextAthleteScorePredictionService>();
        services.AddScoped<IFullContextAthleteScoreModelTrainingService, FullContextAthleteScoreModelTrainingService>();
        services.AddScoped<IFullContextAthleteScoreModelHyperparameterTuningService, FullContextAthleteScoreModelHyperparameterTuningService>();
        services.AddScoped<IFullContextAthleteScorePredictionService, FullContextAthleteScorePredictionService>();
        services.AddScoped<IPredictedLineupOptimizationService, PredictedLineupOptimizationService>();
        return services;
    }
}
