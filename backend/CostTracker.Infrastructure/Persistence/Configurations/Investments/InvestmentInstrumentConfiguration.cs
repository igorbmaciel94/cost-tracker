using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class InvestmentInstrumentConfiguration : IEntityTypeConfiguration<InvestmentInstrument>
{
    public void Configure(EntityTypeBuilder<InvestmentInstrument> entity)
    {
        entity.ToTable("investment_instruments", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_investment_instruments_allocation_score", "allocation_score >= 0");
            tableBuilder.HasCheckConstraint("ck_investment_instruments_quantity_step", "quantity_step IS NULL OR quantity_step > 0");
            tableBuilder.HasCheckConstraint("ck_investment_instruments_version_positive", "version >= 1");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.PortfolioId).HasColumnName("portfolio_id").IsRequired();
        entity.Property(item => item.AssetClass)
            .HasColumnName("asset_class")
            .HasMaxLength(32)
            .HasConversion(
                value => AllocationTargetConfiguration.ToDatabase(value),
                value => AllocationTargetConfiguration.FromDatabase(value))
            .IsRequired();
        entity.Property(item => item.Kind)
            .HasColumnName("kind")
            .HasMaxLength(16)
            .HasConversion(value => KindToDatabase(value), value => KindFromDatabase(value))
            .IsRequired();
        entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        entity.Property(item => item.PublicIdentifier).HasColumnName("public_identifier").HasMaxLength(128);
        entity.Property(item => item.Ticker).HasColumnName("ticker").HasMaxLength(32);
        entity.Property(item => item.Mic).HasColumnName("mic").HasMaxLength(16);
        entity.Property(item => item.Isin).HasColumnName("isin").HasMaxLength(16);
        entity.Property(item => item.IdentityKey).HasColumnName("identity_key").HasMaxLength(256).IsRequired();
        entity.Property(item => item.NativeCurrency)
            .HasColumnName("native_currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.ValuationMode)
            .HasColumnName("valuation_mode")
            .HasMaxLength(16)
            .HasConversion(
                value => value == ValuationMode.MarketQuote ? "MARKET_QUOTE" : "MANUAL",
                value => value == "MARKET_QUOTE" ? ValuationMode.MarketQuote : ValuationMode.Manual)
            .IsRequired();
        entity.Property(item => item.AllocationScore).HasColumnName("allocation_score").IsRequired();
        entity.Property(item => item.QuantityStep).HasColumnName("quantity_step").HasColumnType("numeric(24,12)");
        entity.Property(item => item.IsArchived).HasColumnName("is_archived").IsRequired();
        entity.Property(item => item.ArchivedAt).HasColumnName("archived_at");
        entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();

        entity.HasIndex(item => new { item.PortfolioId, item.IdentityKey })
            .HasFilter("is_archived = FALSE")
            .IsUnique();
        entity.HasIndex(item => new { item.PortfolioId, item.AssetClass, item.IsArchived });

        entity.HasMany(item => item.Transactions)
            .WithOne(item => item.Instrument)
            .HasForeignKey(item => item.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(item => item.ManualValuations)
            .WithOne(item => item.Instrument)
            .HasForeignKey(item => item.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string KindToDatabase(InstrumentKind value) => value switch
    {
        InstrumentKind.Stock => "STOCK",
        InstrumentKind.Etf => "ETF",
        InstrumentKind.Adr => "ADR",
        InstrumentKind.Reit => "REIT",
        InstrumentKind.Bond => "BOND",
        InstrumentKind.Account => "ACCOUNT",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static InstrumentKind KindFromDatabase(string value) => value switch
    {
        "STOCK" => InstrumentKind.Stock,
        "ETF" => InstrumentKind.Etf,
        "ADR" => InstrumentKind.Adr,
        "REIT" => InstrumentKind.Reit,
        "BOND" => InstrumentKind.Bond,
        "ACCOUNT" => InstrumentKind.Account,
        _ => throw new InvalidOperationException($"Unknown investment instrument kind '{value}'.")
    };
}
