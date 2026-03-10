using CostTracker.Api.Services;
using CostTracker.Infrastructure.Persistence;
using CostTracker.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CostTracker.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestUsername = "test-user";
    public const string TestPassword = "test-password";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var passwordHashService = new PasswordHashService();
            var authConfig = new Dictionary<string, string?>
            {
                ["Auth:Username"] = TestUsername,
                ["Auth:PasswordHash"] = passwordHashService.HashPassword(TestUsername, TestPassword)
            };

            configBuilder.AddInMemoryCollection(authConfig);
        });

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
