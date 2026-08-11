using CostTracker.Domain.Entities;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostTracker.Infrastructure.Persistence.Configurations.Investments;

public sealed class InvestmentPortfolioConfiguration : IEntityTypeConfiguration<InvestmentPortfolio>
{
    public void Configure(EntityTypeBuilder<InvestmentPortfolio> entity)
    {
        entity.ToTable("investment_portfolios", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_investment_portfolios_singleton_key", "singleton_key = 1");
            tableBuilder.HasCheckConstraint("ck_investment_portfolios_base_currency_eur", "base_currency = 'EUR'");
            tableBuilder.HasCheckConstraint("ck_investment_portfolios_version_positive", "version >= 1");
        });

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.SingletonKey).HasColumnName("singleton_key").IsRequired();
        entity.Property(item => item.BaseCurrency)
            .HasColumnName("base_currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasConversion(value => value.Value, value => new CurrencyCode(value))
            .IsRequired();
        entity.Property(item => item.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();

        entity.HasIndex(item => item.SingletonKey).IsUnique();
        entity.HasMany(item => item.AllocationTargets)
            .WithOne(item => item.Portfolio)
            .HasForeignKey(item => item.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(item => item.Instruments)
            .WithOne(item => item.Portfolio)
            .HasForeignKey(item => item.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
