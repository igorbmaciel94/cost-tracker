using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentPortfolioFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investment_portfolios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    singleton_key = table.Column<byte>(type: "smallint", nullable: false),
                    base_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_portfolios", x => x.id);
                    table.CheckConstraint("ck_investment_portfolios_base_currency_eur", "base_currency = 'EUR'");
                    table.CheckConstraint("ck_investment_portfolios_singleton_key", "singleton_key = 1");
                    table.CheckConstraint("ck_investment_portfolios_version_positive", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "investment_allocation_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(9,8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_allocation_targets", x => x.id);
                    table.CheckConstraint("ck_investment_allocation_targets_weight", "weight >= 0 AND weight <= 1");
                    table.ForeignKey(
                        name: "FK_investment_allocation_targets_investment_portfolios_portfol~",
                        column: x => x.portfolio_id,
                        principalTable: "investment_portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "investment_instruments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    public_identifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    mic = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    isin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    identity_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    native_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    valuation_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    allocation_score = table.Column<int>(type: "integer", nullable: false),
                    quantity_step = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_instruments", x => x.id);
                    table.CheckConstraint("ck_investment_instruments_allocation_score", "allocation_score >= 0");
                    table.CheckConstraint("ck_investment_instruments_quantity_step", "quantity_step IS NULL OR quantity_step > 0");
                    table.CheckConstraint("ck_investment_instruments_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "FK_investment_instruments_investment_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "investment_portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "investment_manual_valuations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    as_of = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_manual_valuations", x => x.id);
                    table.CheckConstraint("ck_investment_manual_valuations_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "FK_investment_manual_valuations_investment_instruments_instrum~",
                        column: x => x.instrument_id,
                        principalTable: "investment_instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "investment_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(20,8)", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(20,8)", nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    fee_amount = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    currency_per_eur_rate = table.Column<decimal>(type: "numeric(20,10)", nullable: true),
                    notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_transactions", x => x.id);
                    table.CheckConstraint("ck_investment_transactions_fee_non_negative", "fee_amount >= 0");
                    table.CheckConstraint("ck_investment_transactions_fx_positive", "currency_per_eur_rate IS NULL OR currency_per_eur_rate > 0");
                    table.ForeignKey(
                        name: "FK_investment_transactions_investment_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "investment_instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investment_allocation_targets_portfolio_id_asset_class",
                table: "investment_allocation_targets",
                columns: new[] { "portfolio_id", "asset_class" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_instruments_portfolio_id_asset_class_is_archived",
                table: "investment_instruments",
                columns: new[] { "portfolio_id", "asset_class", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_instruments_portfolio_id_identity_key",
                table: "investment_instruments",
                columns: new[] { "portfolio_id", "identity_key" },
                unique: true,
                filter: "is_archived = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_investment_manual_valuations_instrument_id_as_of",
                table: "investment_manual_valuations",
                columns: new[] { "instrument_id", "as_of" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_manual_valuations_instrument_id_idempotency_key",
                table: "investment_manual_valuations",
                columns: new[] { "instrument_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_portfolios_singleton_key",
                table: "investment_portfolios",
                column: "singleton_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_transactions_instrument_id_idempotency_key",
                table: "investment_transactions",
                columns: new[] { "instrument_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_transactions_instrument_id_transaction_date",
                table: "investment_transactions",
                columns: new[] { "instrument_id", "transaction_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investment_allocation_targets");

            migrationBuilder.DropTable(
                name: "investment_manual_valuations");

            migrationBuilder.DropTable(
                name: "investment_transactions");

            migrationBuilder.DropTable(
                name: "investment_instruments");

            migrationBuilder.DropTable(
                name: "investment_portfolios");
        }
    }
}
