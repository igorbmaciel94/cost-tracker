using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class AllocationTargetConfiguration : IEntityTypeConfiguration<AllocationTarget>
{
    public void Configure(EntityTypeBuilder<AllocationTarget> entity)
    {
        entity.ToTable("investment_allocation_targets", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_investment_allocation_targets_weight", "weight >= 0 AND weight <= 1");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.PortfolioId).HasColumnName("portfolio_id").IsRequired();
        entity.Property(item => item.AssetClass)
            .HasColumnName("asset_class")
            .HasMaxLength(32)
            .HasConversion(
                value => ToDatabase(value),
                value => FromDatabase(value))
            .IsRequired();
        entity.Property(item => item.Weight).HasColumnName("weight").HasColumnType("numeric(9,8)").IsRequired();

        entity.HasIndex(item => new { item.PortfolioId, item.AssetClass }).IsUnique();
    }

    internal static string ToDatabase(AssetClass value) => value switch
    {
        AssetClass.Stocks => "STOCKS",
        AssetClass.Reits => "REITS",
        AssetClass.BrazilFixedIncome => "BRAZIL_FIXED_INCOME",
        AssetClass.InternationalFixedIncome => "INTERNATIONAL_FIXED_INCOME",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    internal static AssetClass FromDatabase(string value) => value switch
    {
        "STOCKS" => AssetClass.Stocks,
        "REITS" => AssetClass.Reits,
        "BRAZIL_FIXED_INCOME" => AssetClass.BrazilFixedIncome,
        "INTERNATIONAL_FIXED_INCOME" => AssetClass.InternationalFixedIncome,
        _ => throw new InvalidOperationException($"Unknown investment asset class '{value}'.")
    };
}
