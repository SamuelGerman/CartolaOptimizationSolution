using CartolaOptimization.Infrastructure;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Api.Middleware;
using CartolaOptimization.Api.Swagger;
using CartolaOptimization.ML;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

var currentDirectory = Directory.GetCurrentDirectory();
var currentDirectoryHasAppSettings = File.Exists(Path.Combine(currentDirectory, "appsettings.json"));
var contentRootPath = currentDirectoryHasAppSettings
    ? currentDirectory
    : AppContext.BaseDirectory;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRootPath,
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var mySqlServerVersion = new MySqlServerVersion(new Version(8, 0, 36));
Action<DbContextOptionsBuilder> configureDbContext = options =>
{
    options.UseMySql(
        connectionString,
        mySqlServerVersion,
        mysqlOptions =>
        {
            mysqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        });
};

builder.Services.AddDbContext<AppDbContext>(
    configureDbContext,
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Singleton);
builder.Services.AddDbContextFactory<AppDbContext>(configureDbContext);

builder.Services.AddControllers();
builder.Services.AddInfrastructureServices();
builder.Services.AddMachineLearningServices(builder.Configuration);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Cartola Optimization API",
        Version = "v1",
        Description = "Administrative endpoints used to synchronize external football datasets and feed the optimization pipeline.",
    });

    var xmlFiles = new[]
    {
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml",
        "CartolaOptimization.Application.xml",
        "CartolaOptimization.Domain.xml",
        "CartolaOptimization.Infrastructure.xml",
    };

    foreach (var xmlFile in xmlFiles)
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    }

    options.SchemaFilter<PredictedLineupRequestSchemaFilter>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ApiExceptionHandlingMiddleware>();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
