using CostTracker.Domain.Entities;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class ManualValuationConfiguration : IEntityTypeConfiguration<ManualValuation>
{
    public void Configure(EntityTypeBuilder<ManualValuation> entity)
    {
        entity.ToTable("investment_manual_valuations", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_investment_manual_valuations_amount", "amount >= 0");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.InstrumentId).HasColumnName("instrument_id").IsRequired();
        entity.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(20,8)").IsRequired();
        entity.Property(item => item.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.AsOf).HasColumnName("as_of").HasColumnType("date").IsRequired();
        entity.Property(item => item.RecordedAt).HasColumnName("recorded_at").IsRequired();
        entity.Property(item => item.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();

        entity.HasIndex(item => new { item.InstrumentId, item.IdempotencyKey }).IsUnique();
        entity.HasIndex(item => new { item.InstrumentId, item.AsOf });
    }
}
