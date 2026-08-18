using CostTracker.Application.Interfaces;
using CostTracker.Application.Investments.Contributions;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Infrastructure.Persistence;

public class CostTrackerDbContext(DbContextOptions<CostTrackerDbContext> options) : DbContext(options), ICostTrackerDbContext, IContributionPlanningDbContext
{
    public DbSet<Month> Months => Set<Month>();
    public DbSet<CategoryBudget> CategoryBudgets => Set<CategoryBudget>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<GroupTarget> GroupTargets => Set<GroupTarget>();
    public DbSet<PlanningGoal> PlanningGoals => Set<PlanningGoal>();
    public DbSet<HealthProfile> HealthProfiles => Set<HealthProfile>();
    public DbSet<InvestmentPortfolio> InvestmentPortfolios => Set<InvestmentPortfolio>();
    public DbSet<AllocationTarget> InvestmentAllocationTargets => Set<AllocationTarget>();
    public DbSet<InvestmentInstrument> InvestmentInstruments => Set<InvestmentInstrument>();
    public DbSet<InvestmentTransaction> InvestmentTransactions => Set<InvestmentTransaction>();
    public DbSet<ManualValuation> ManualValuations => Set<ManualValuation>();
    public DbSet<MarketInstrumentMapping> MarketInstrumentMappings => Set<MarketInstrumentMapping>();
    public DbSet<MarketQuoteSnapshot> MarketQuoteSnapshots => Set<MarketQuoteSnapshot>();
    public DbSet<FxRateSnapshot> FxRateSnapshots => Set<FxRateSnapshot>();
    public DbSet<ContributionPlan> ContributionPlans => Set<ContributionPlan>();
    public DbSet<ContributionPlanLine> ContributionPlanLines => Set<ContributionPlanLine>();
    public DbSet<DividendEvent> DividendEvents => Set<DividendEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CostTrackerDbContext).Assembly);

        modelBuilder.Entity<Month>(entity =>
        {
            entity.ToTable("months");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ReferenceMonth).HasColumnName("reference_month").HasMaxLength(7).IsRequired();
            entity.Property(x => x.Salary).HasColumnName("salary").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.ClosedAt).HasColumnName("closed_at");
            entity.Property(x => x.ClonedFromMonthId).HasColumnName("cloned_from_month_id");
            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion(
                    value => value == MonthStatus.Open ? "OPEN" : "CLOSED",
                    value => string.Equals(value, "OPEN", StringComparison.OrdinalIgnoreCase) ? MonthStatus.Open : MonthStatus.Closed)
                .IsRequired();

            entity.HasIndex(x => x.ReferenceMonth).IsUnique();
            entity.HasIndex(x => x.Status).HasFilter("\"status\" = 'OPEN'").IsUnique();

            entity.HasMany(x => x.CategoryBudgets)
                .WithOne(x => x.Month)
                .HasForeignKey(x => x.MonthId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Entries)
                .WithOne(x => x.Month)
                .HasForeignKey(x => x.MonthId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.GroupTargets)
                .WithOne(x => x.Month)
                .HasForeignKey(x => x.MonthId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryBudget>(entity =>
        {
            entity.ToTable("category_budgets", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_category_budgets_planned_amount_non_negative", "planned_amount >= 0");
            });
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.MonthId).HasColumnName("month_id").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            entity.Property(x => x.GroupName).HasColumnName("group_name").HasMaxLength(64).IsRequired();
            entity.Property(x => x.PlannedAmount).HasColumnName("planned_amount").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired();

            entity.HasIndex(x => new { x.MonthId, x.Name }).IsUnique();

            entity.HasMany(x => x.Entries)
                .WithOne(x => x.CategoryBudget)
                .HasForeignKey(x => x.CategoryBudgetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Entry>(entity =>
        {
            entity.ToTable("entries", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_entries_amount_non_negative", "amount >= 0");
            });
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.MonthId).HasColumnName("month_id").IsRequired();
            entity.Property(x => x.CategoryBudgetId).HasColumnName("category_budget_id").IsRequired();
            entity.Property(x => x.EntryDate).HasColumnName("entry_date").HasColumnType("date").IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(256).IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(x => new { x.MonthId, x.EntryDate });
        });

        modelBuilder.Entity<GroupTarget>(entity =>
        {
            entity.ToTable("group_targets");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.MonthId).HasColumnName("month_id").IsRequired();
            entity.Property(x => x.GroupName).HasColumnName("group_name").HasMaxLength(64).IsRequired();
            entity.Property(x => x.TargetPercent).HasColumnName("target_percent").HasColumnType("numeric(5,4)").IsRequired();

            entity.HasIndex(x => new { x.MonthId, x.GroupName }).IsUnique();
        });

        modelBuilder.Entity<PlanningGoal>(entity =>
        {
            entity.ToTable("planning_goals", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_planning_goals_total_amount_non_negative", "total_amount >= 0");
                tableBuilder.HasCheckConstraint("ck_planning_goals_saved_amount_non_negative", "saved_amount >= 0");
                tableBuilder.HasCheckConstraint("ck_planning_goals_months_positive", "months >= 1");
            });
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(x => x.SavedAmount).HasColumnName("saved_amount").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(x => x.Months).HasColumnName("months").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<HealthProfile>(entity =>
        {
            entity.ToTable("health_profiles", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_health_profiles_essential_expenses", "essential_expenses >= 0");
                tableBuilder.HasCheckConstraint("ck_health_profiles_saved_emergency_fund", "saved_emergency_fund >= 0");
            });
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.EssentialExpenses).HasColumnName("essential_expenses").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(x => x.SavedEmergencyFund).HasColumnName("saved_emergency_fund").HasColumnType("numeric(12,2)").IsRequired();
        });
    }
}
