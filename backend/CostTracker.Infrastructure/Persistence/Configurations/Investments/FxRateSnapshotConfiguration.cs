using CostTracker.Domain.Entities;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class FxRateSnapshotConfiguration : IEntityTypeConfiguration<FxRateSnapshot>
{
    public void Configure(EntityTypeBuilder<FxRateSnapshot> entity)
    {
        entity.ToTable("fx_rate_snapshots", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_fx_rate_snapshots_rate", "rate > 0");
            tableBuilder.HasCheckConstraint(
                "ck_fx_rate_snapshots_distinct_currencies",
                "base_currency <> quote_currency");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.ProviderCode).HasColumnName("provider_code").HasMaxLength(32).IsRequired();
        entity.Property(item => item.BaseCurrency)
            .HasColumnName("base_currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.QuoteCurrency)
            .HasColumnName("quote_currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.Rate).HasColumnName("rate").HasColumnType("numeric(24,12)").IsRequired();
        entity.Property(item => item.RateKind).HasColumnName("rate_kind").HasMaxLength(32).IsRequired();
        entity.Property(item => item.AsOf).HasColumnName("as_of").HasColumnType("date").IsRequired();
        entity.Property(item => item.FetchedAt).HasColumnName("fetched_at").IsRequired();
        entity.Property(item => item.IsFallback).HasColumnName("is_fallback").IsRequired();
        entity.Property(item => item.RawPayloadHash).HasColumnName("raw_payload_hash").HasMaxLength(64).IsRequired();

        entity.HasIndex(item => new { item.BaseCurrency, item.QuoteCurrency, item.AsOf, item.ProviderCode });
        entity.HasIndex(item => new { item.BaseCurrency, item.QuoteCurrency, item.AsOf });
    }
}
