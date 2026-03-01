using CostTracker.Infrastructure.Persistence;
using CostTracker.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CostTracker.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var databaseName = $"cost-tracker-tests-{Guid.NewGuid()}";

            services.RemoveAll(typeof(DbContextOptions<CostTrackerDbContext>));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<CostTrackerDbContext>));
            services.RemoveAll(typeof(DbContextOptions));

            services.AddDbContext<CostTrackerDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CostTrackerDbContext>();
            dbContext.Database.EnsureCreated();

            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            seeder.SeedAsync().GetAwaiter().GetResult();
        });
    }
}
