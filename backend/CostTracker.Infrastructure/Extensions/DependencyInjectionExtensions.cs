using CostTracker.Infrastructure.Persistence;
using CostTracker.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CostTracker.Infrastructure.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=costtracker;Username=costtracker;Password=costtracker";

        services.AddDbContext<CostTrackerDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(CostTrackerDbContext).Assembly.FullName);
            }));

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<CanonicalDataBackfillService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CostTrackerDbContext>();

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();

        var backfill = scope.ServiceProvider.GetRequiredService<CanonicalDataBackfillService>();
        await backfill.ApplyAsync();
    }
}
