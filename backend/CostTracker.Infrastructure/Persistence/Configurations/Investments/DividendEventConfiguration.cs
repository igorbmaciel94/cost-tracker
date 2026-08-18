using CostTracker.Domain.Entities;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class DividendEventConfiguration : IEntityTypeConfiguration<DividendEvent>
{
    public void Configure(EntityTypeBuilder<DividendEvent> entity)
    {
        entity.ToTable("dividend_events", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_dividend_events_amount_per_unit_positive", "gross_amount_per_unit > 0");
            tableBuilder.HasCheckConstraint("ck_dividend_events_tax_rate", "withholding_tax_rate >= 0 AND withholding_tax_rate < 1");
            tableBuilder.HasCheckConstraint("ck_dividend_events_payment_after_ex", "payment_date >= ex_date");
            tableBuilder.HasCheckConstraint("ck_dividend_events_eligible_quantity", "eligible_quantity IS NULL OR eligible_quantity >= 0");
            tableBuilder.HasCheckConstraint("ck_dividend_events_amounts", "(gross_amount IS NULL OR gross_amount >= 0) AND (withholding_tax_amount IS NULL OR withholding_tax_amount >= 0) AND (net_amount IS NULL OR net_amount >= 0)");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.InstrumentId).HasColumnName("instrument_id").IsRequired();
        entity.Property(item => item.GrossAmountPerUnit).HasColumnName("gross_amount_per_unit").HasColumnType("numeric(24,12)").IsRequired();
        entity.Property(item => item.WithholdingTaxRate).HasColumnName("withholding_tax_rate").HasColumnType("numeric(9,8)").IsRequired();
        entity.Property(item => item.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.ExDate).HasColumnName("ex_date").HasColumnType("date").IsRequired();
        entity.Property(item => item.PaymentDate).HasColumnName("payment_date").HasColumnType("date").IsRequired();
        entity.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(512);
        entity.Property(item => item.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        entity.Property(item => item.EligibleQuantity).HasColumnName("eligible_quantity").HasColumnType("numeric(24,12)");
        entity.Property(item => item.GrossAmount).HasColumnName("gross_amount").HasColumnType("numeric(24,12)");
        entity.Property(item => item.WithholdingTaxAmount).HasColumnName("withholding_tax_amount").HasColumnType("numeric(24,12)");
        entity.Property(item => item.NetAmount).HasColumnName("net_amount").HasColumnType("numeric(24,12)");
        entity.Property(item => item.CurrencyPerEurRate).HasColumnName("currency_per_eur_rate").HasColumnType("numeric(24,12)");
        entity.Property(item => item.NetAmountEur).HasColumnName("net_amount_eur").HasColumnType("numeric(24,12)");
        entity.Property(item => item.FxAsOf).HasColumnName("fx_as_of").HasColumnType("date");
        entity.Property(item => item.FxProviderCode).HasColumnName("fx_provider_code").HasMaxLength(32);
        entity.Property(item => item.ProcessedAt).HasColumnName("processed_at");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();

        entity.HasIndex(item => item.IdempotencyKey).IsUnique();
        entity.HasIndex(item => new { item.InstrumentId, item.ExDate, item.PaymentDate });
        entity.HasIndex(item => new { item.ProcessedAt, item.PaymentDate });

        entity.HasOne(item => item.Instrument)
            .WithMany(item => item.DividendEvents)
            .HasForeignKey(item => item.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
