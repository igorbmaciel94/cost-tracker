using CostTracker.Domain.Entities;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class MarketQuoteSnapshotConfiguration : IEntityTypeConfiguration<MarketQuoteSnapshot>
{
    public void Configure(EntityTypeBuilder<MarketQuoteSnapshot> entity)
    {
        entity.ToTable("market_quote_snapshots", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_market_quote_snapshots_price", "price > 0");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.InstrumentId).HasColumnName("instrument_id").IsRequired();
        entity.Property(item => item.ProviderCode).HasColumnName("provider_code").HasMaxLength(32).IsRequired();
        entity.Property(item => item.ProviderSymbol).HasColumnName("provider_symbol").HasMaxLength(128).IsRequired();
        entity.Property(item => item.Exchange).HasColumnName("exchange").HasMaxLength(64);
        entity.Property(item => item.Mic).HasColumnName("mic").HasMaxLength(16);
        entity.Property(item => item.Price).HasColumnName("price").HasColumnType("numeric(24,12)").IsRequired();
        entity.Property(item => item.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.PriceKind).HasColumnName("price_kind").HasMaxLength(32).IsRequired();
        entity.Property(item => item.AsOf).HasColumnName("as_of").HasColumnType("date").IsRequired();
        entity.Property(item => item.FetchedAt).HasColumnName("fetched_at").IsRequired();
        entity.Property(item => item.IsFallback).HasColumnName("is_fallback").IsRequired();
        entity.Property(item => item.RawPayloadHash).HasColumnName("raw_payload_hash").HasMaxLength(64).IsRequired();

        entity.HasIndex(item => new { item.InstrumentId, item.AsOf, item.ProviderCode });
        entity.HasIndex(item => new { item.InstrumentId, item.AsOf });

        entity.HasOne(item => item.Instrument)
            .WithMany()
            .HasForeignKey(item => item.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
