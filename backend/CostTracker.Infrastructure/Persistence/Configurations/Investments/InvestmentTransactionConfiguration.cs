using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class InvestmentTransactionConfiguration : IEntityTypeConfiguration<InvestmentTransaction>
{
    public void Configure(EntityTypeBuilder<InvestmentTransaction> entity)
    {
        entity.ToTable("investment_transactions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_investment_transactions_fee_non_negative", "fee_amount >= 0");
            tableBuilder.HasCheckConstraint("ck_investment_transactions_fx_positive", "currency_per_eur_rate IS NULL OR currency_per_eur_rate > 0");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.InstrumentId).HasColumnName("instrument_id").IsRequired();
        entity.Property(item => item.Type)
            .HasColumnName("transaction_type")
            .HasMaxLength(24)
            .HasConversion(value => ToDatabase(value), value => FromDatabase(value))
            .IsRequired();
        entity.Property(item => item.TransactionDate).HasColumnName("transaction_date").HasColumnType("date").IsRequired();
        entity.Property(item => item.Quantity).HasColumnName("quantity").HasColumnType("numeric(24,12)");
        entity.Property(item => item.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(20,8)");
        entity.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(20,8)");
        entity.Property(item => item.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.FeeAmount).HasColumnName("fee_amount").HasColumnType("numeric(20,8)").IsRequired();
        entity.Property(item => item.CurrencyPerEurRate).HasColumnName("currency_per_eur_rate").HasColumnType("numeric(20,10)");
        entity.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(512);
        entity.Property(item => item.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasIndex(item => new { item.InstrumentId, item.IdempotencyKey }).IsUnique();
        entity.HasIndex(item => new { item.InstrumentId, item.TransactionDate });
    }

    private static string ToDatabase(InvestmentTransactionType value) => value switch
    {
        InvestmentTransactionType.OpeningBalance => "OPENING_BALANCE",
        InvestmentTransactionType.Buy => "BUY",
        InvestmentTransactionType.Sell => "SELL",
        InvestmentTransactionType.Deposit => "DEPOSIT",
        InvestmentTransactionType.Withdrawal => "WITHDRAWAL",
        InvestmentTransactionType.Adjustment => "ADJUSTMENT",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static InvestmentTransactionType FromDatabase(string value) => value switch
    {
        "OPENING_BALANCE" => InvestmentTransactionType.OpeningBalance,
        "BUY" => InvestmentTransactionType.Buy,
        "SELL" => InvestmentTransactionType.Sell,
        "DEPOSIT" => InvestmentTransactionType.Deposit,
        "WITHDRAWAL" => InvestmentTransactionType.Withdrawal,
        "ADJUSTMENT" => InvestmentTransactionType.Adjustment,
        _ => throw new InvalidOperationException($"Unknown investment transaction type '{value}'.")
    };
}
