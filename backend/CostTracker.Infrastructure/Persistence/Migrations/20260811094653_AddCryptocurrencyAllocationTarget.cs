using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCryptocurrencyAllocationTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH inserted AS (
                    INSERT INTO investment_allocation_targets (id, portfolio_id, asset_class, weight)
                    SELECT
                        md5(portfolio.id::text || ':CRYPTOCURRENCIES')::uuid,
                        portfolio.id,
                        'CRYPTOCURRENCIES',
                        0
                    FROM investment_portfolios AS portfolio
                    ON CONFLICT (portfolio_id, asset_class) DO NOTHING
                    RETURNING portfolio_id
                )
                UPDATE investment_portfolios AS portfolio
                SET
                    version = portfolio.version + 1,
                    updated_at = CURRENT_TIMESTAMP
                FROM inserted
                WHERE portfolio.id = inserted.portfolio_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE investment_allocation_targets AS destination
                SET weight = destination.weight + cryptocurrency.weight
                FROM investment_allocation_targets AS cryptocurrency
                WHERE destination.portfolio_id = cryptocurrency.portfolio_id
                  AND destination.asset_class = 'INTERNATIONAL_FIXED_INCOME'
                  AND cryptocurrency.asset_class = 'CRYPTOCURRENCIES';
                """);

            migrationBuilder.Sql(
                """
                WITH removed AS (
                    DELETE FROM investment_allocation_targets
                    WHERE asset_class = 'CRYPTOCURRENCIES'
                    RETURNING portfolio_id
                )
                UPDATE investment_portfolios AS portfolio
                SET
                    version = portfolio.version + 1,
                    updated_at = CURRENT_TIMESTAMP
                FROM removed
                WHERE portfolio.id = removed.portfolio_id;
                """);
        }
    }
}
