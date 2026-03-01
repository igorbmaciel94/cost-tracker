using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CostTracker.Infrastructure.Persistence;

public class CostTrackerDbContextFactory : IDesignTimeDbContextFactory<CostTrackerDbContext>
{
    public CostTrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CostTrackerDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=costtracker;Username=costtracker;Password=costtracker";

        optionsBuilder.UseNpgsql(connectionString);

        return new CostTrackerDbContext(optionsBuilder.Options);
    }
}
