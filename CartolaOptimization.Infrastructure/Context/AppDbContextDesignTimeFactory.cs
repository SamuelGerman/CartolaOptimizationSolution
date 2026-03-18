using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace CartolaOptimization.Infrastructure.Context;

/// <summary>
/// Creates <see cref="AppDbContext"/> instances for EF Core design-time commands without depending on the API host startup path.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var apiDirectory = ResolveApiDirectory();
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            TryReadConnectionString(Path.Combine(apiDirectory, "appsettings.Development.json")) ??
            TryReadConnectionString(Path.Combine(apiDirectory, "appsettings.json")) ??
            "Server=localhost;Database=cartola_db;Uid=root;Pwd=root;";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 36)),
            mysqlOptions => mysqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var apiDirectory = Path.GetFullPath(Path.Combine(currentDirectory, "..", "CartolaOptimization.Api"));

        return Directory.Exists(apiDirectory)
            ? apiDirectory
            : currentDirectory;
    }

    private static string? TryReadConnectionString(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) ||
            !connectionStrings.TryGetProperty("DefaultConnection", out var connectionString))
        {
            return null;
        }

        return connectionString.GetString();
    }
}
