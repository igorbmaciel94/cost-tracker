using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class ContributionPlanLineConfiguration : IEntityTypeConfiguration<ContributionPlanLine>
{
    public void Configure(EntityTypeBuilder<ContributionPlanLine> entity)
    {
        entity.ToTable("investment_contribution_plan_lines", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plan_lines_amounts",
                "current_value_eur >= 0 AND recommended_amount_eur >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plan_lines_target",
                "target_weight >= 0 AND target_weight <= 1");
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plan_lines_native_amount",
                "recommended_native_amount IS NULL OR recommended_native_amount >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plan_lines_quantity",
                "suggested_quantity IS NULL OR suggested_quantity >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plan_lines_price",
                "unit_price IS NULL OR unit_price > 0");
            tableBuilder.HasCheckConstraint(
                "ck_investment_contribution_plan_lines_fx",
                "native_currency_per_eur IS NULL OR native_currency_per_eur > 0");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.ContributionPlanId).HasColumnName("contribution_plan_id").IsRequired();
        entity.Property(item => item.AssetClass)
            .HasColumnName("asset_class")
            .HasMaxLength(32)
            .HasConversion(
                value => AllocationTargetConfiguration.ToDatabase(value),
                value => AllocationTargetConfiguration.FromDatabase(value))
            .IsRequired();
        entity.Property(item => item.InstrumentId).HasColumnName("instrument_id");
        entity.Property(item => item.InstrumentName).HasColumnName("instrument_name").HasMaxLength(160);
        entity.Property(item => item.Ticker).HasColumnName("ticker").HasMaxLength(32);
        entity.Property(item => item.NativeCurrency).HasColumnName("native_currency").HasMaxLength(3).IsFixedLength();
        entity.Property(item => item.CurrentValueEur).HasColumnName("current_value_eur").HasColumnType("numeric(20,8)").IsRequired();
        entity.Property(item => item.TargetWeight).HasColumnName("target_weight").HasColumnType("numeric(9,8)").IsRequired();
        entity.Property(item => item.RecommendedAmountEur).HasColumnName("recommended_amount_eur").HasColumnType("numeric(20,8)").IsRequired();
        entity.Property(item => item.RecommendedNativeAmount).HasColumnName("recommended_native_amount").HasColumnType("numeric(20,8)");
        entity.Property(item => item.SuggestedQuantity).HasColumnName("suggested_quantity").HasColumnType("numeric(24,12)");
        entity.Property(item => item.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(24,12)");
        entity.Property(item => item.AllocationScore).HasColumnName("allocation_score");
        entity.Property(item => item.Explanation).HasColumnName("explanation").HasMaxLength(1024).IsRequired();
        entity.Property(item => item.QuoteSnapshotId).HasColumnName("quote_snapshot_id");
        entity.Property(item => item.QuoteAsOf).HasColumnName("quote_as_of").HasColumnType("date");
        entity.Property(item => item.FxSnapshotId).HasColumnName("fx_snapshot_id");
        entity.Property(item => item.FxAsOf).HasColumnName("fx_as_of").HasColumnType("date");
        entity.Property(item => item.NativeCurrencyPerEur).HasColumnName("native_currency_per_eur").HasColumnType("numeric(24,12)");
        entity.Property(item => item.Freshness)
            .HasColumnName("freshness")
            .HasMaxLength(16)
            .HasConversion(value => ToDatabase(value), value => FromDatabase(value))
            .IsRequired();

        entity.HasIndex(item => new { item.ContributionPlanId, item.AssetClass });
        entity.HasIndex(item => item.InstrumentId);

        entity.HasOne(item => item.Instrument)
            .WithMany()
            .HasForeignKey(item => item.InstrumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static string ToDatabase(ContributionDataFreshness value) => value switch
    {
        ContributionDataFreshness.Fresh => "FRESH",
        ContributionDataFreshness.Stale => "STALE",
        ContributionDataFreshness.Blocked => "BLOCKED",
        ContributionDataFreshness.Missing => "MISSING",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static ContributionDataFreshness FromDatabase(string value) => value switch
    {
        "FRESH" => ContributionDataFreshness.Fresh,
        "STALE" => ContributionDataFreshness.Stale,
        "BLOCKED" => ContributionDataFreshness.Blocked,
        "MISSING" => ContributionDataFreshness.Missing,
        _ => throw new InvalidOperationException($"Unknown contribution data freshness '{value}'.")
    };
}
