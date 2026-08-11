using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class ContributionPlanConfiguration : IEntityTypeConfiguration<ContributionPlan>
{
    public void Configure(EntityTypeBuilder<ContributionPlan> entity)
    {
        entity.ToTable("investment_contribution_plans", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plans_amount_positive",
                "contribution_amount_eur > 0");
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plans_totals",
                "total_suggested_eur >= 0 AND residual_amount_eur >= 0 AND total_suggested_eur + residual_amount_eur = contribution_amount_eur");
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plans_expiration",
                "expires_at > created_at");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.PortfolioId).HasColumnName("portfolio_id").IsRequired();
        entity.Property(item => item.PortfolioVersion).HasColumnName("portfolio_version").IsRequired();
        entity.Property(item => item.PolicyVersion).HasColumnName("policy_version").HasMaxLength(64).IsRequired();
        entity.Property(item => item.StrategyVersion).HasColumnName("strategy_version").HasMaxLength(64).IsRequired();
        entity.Property(item => item.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .HasConversion(value => ToDatabase(value), value => FromDatabase(value))
            .IsRequired();
        entity.Property(item => item.ContributionAmountEur)
            .HasColumnName("contribution_amount_eur")
            .HasColumnType("numeric(20,8)")
            .IsRequired();
        entity.Property(item => item.TotalSuggestedEur)
            .HasColumnName("total_suggested_eur")
            .HasColumnType("numeric(20,8)")
            .IsRequired();
        entity.Property(item => item.ResidualAmountEur)
            .HasColumnName("residual_amount_eur")
            .HasColumnType("numeric(20,8)")
            .IsRequired();
        entity.Property(item => item.AllowedStaleData).HasColumnName("allowed_stale_data").IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(item => item.ExpiresAt).HasColumnName("expires_at").IsRequired();
        entity.Property(item => item.ConfirmedAt).HasColumnName("confirmed_at");
        entity.Property(item => item.ConfirmationIdempotencyKey)
            .HasColumnName("confirmation_idempotency_key")
            .HasMaxLength(128);

        entity.HasIndex(item => new { item.PortfolioId, item.CreatedAt });
        entity.HasIndex(item => item.ConfirmationIdempotencyKey)
            .HasFilter("confirmation_idempotency_key IS NOT NULL")
            .IsUnique();

        entity.HasOne(item => item.Portfolio)
            .WithMany()
            .HasForeignKey(item => item.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(item => item.Lines)
            .WithOne(item => item.ContributionPlan)
            .HasForeignKey(item => item.ContributionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string ToDatabase(ContributionPlanStatus value) => value switch
    {
        ContributionPlanStatus.Draft => "DRAFT",
        ContributionPlanStatus.Confirmed => "CONFIRMED",
        ContributionPlanStatus.Expired => "EXPIRED",
        ContributionPlanStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static ContributionPlanStatus FromDatabase(string value) => value switch
    {
        "DRAFT" => ContributionPlanStatus.Draft,
        "CONFIRMED" => ContributionPlanStatus.Confirmed,
        "EXPIRED" => ContributionPlanStatus.Expired,
        "CANCELLED" => ContributionPlanStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unknown contribution plan status '{value}'.")
    };
}
