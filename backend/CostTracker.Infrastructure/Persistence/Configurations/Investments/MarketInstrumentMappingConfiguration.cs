using CostTracker.Domain.Entities;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class MarketInstrumentMappingConfiguration : IEntityTypeConfiguration<MarketInstrumentMapping>
{
    public void Configure(EntityTypeBuilder<MarketInstrumentMapping> entity)
    {
        entity.ToTable("market_instrument_mappings", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_market_instrument_mappings_price_multiplier",
                "price_multiplier > 0");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.InstrumentId).HasColumnName("instrument_id").IsRequired();
        entity.Property(item => item.ProviderCode).HasColumnName("provider_code").HasMaxLength(32).IsRequired();
        entity.Property(item => item.ProviderSymbol).HasColumnName("provider_symbol").HasMaxLength(128).IsRequired();
        entity.Property(item => item.Exchange).HasColumnName("exchange").HasMaxLength(64);
        entity.Property(item => item.Mic).HasColumnName("mic").HasMaxLength(16);
        entity.Property(item => item.QuoteCurrency)
            .HasColumnName("quote_currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.PriceMultiplier)
            .HasColumnName("price_multiplier")
            .HasColumnType("numeric(24,12)")
            .IsRequired();
        entity.Property(item => item.IsEnabled).HasColumnName("is_enabled").IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();

        entity.HasIndex(item => new { item.InstrumentId, item.ProviderCode }).IsUnique();
        entity.HasIndex(item => new { item.ProviderCode, item.ProviderSymbol, item.Exchange });

        entity.HasOne(item => item.Instrument)
            .WithMany()
            .HasForeignKey(item => item.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
