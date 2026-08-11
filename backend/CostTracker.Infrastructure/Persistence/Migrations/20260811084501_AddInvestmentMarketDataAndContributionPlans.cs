using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentMarketDataAndContributionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fx_rate_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    base_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    quote_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(24,12)", nullable: false),
                    rate_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    as_of = table.Column<DateOnly>(type: "date", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_fallback = table.Column<bool>(type: "boolean", nullable: false),
                    raw_payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fx_rate_snapshots", x => x.id);
                    table.CheckConstraint("ck_fx_rate_snapshots_distinct_currencies", "base_currency <> quote_currency");
                    table.CheckConstraint("ck_fx_rate_snapshots_rate", "rate > 0");
                });

            migrationBuilder.CreateTable(
                name: "investment_contribution_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_version = table.Column<long>(type: "bigint", nullable: false),
                    policy_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    strategy_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    contribution_amount_eur = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    total_suggested_eur = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    residual_amount_eur = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    allowed_stale_data = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmation_idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_contribution_plans", x => x.id);
                    table.CheckConstraint("ck_investment_contribution_plans_amount_positive", "contribution_amount_eur > 0");
                    table.CheckConstraint("ck_investment_contribution_plans_expiration", "expires_at > created_at");
                    table.CheckConstraint("ck_investment_contribution_plans_totals", "total_suggested_eur >= 0 AND residual_amount_eur >= 0 AND total_suggested_eur + residual_amount_eur = contribution_amount_eur");
                    table.ForeignKey(
                        name: "FK_investment_contribution_plans_investment_portfolios_portfol~",
                        column: x => x.portfolio_id,
                        principalTable: "investment_portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "market_instrument_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_symbol = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    exchange = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mic = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    quote_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    price_multiplier = table.Column<decimal>(type: "numeric(24,12)", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_instrument_mappings", x => x.id);
                    table.CheckConstraint("ck_market_instrument_mappings_price_multiplier", "price_multiplier > 0");
                    table.ForeignKey(
                        name: "FK_market_instrument_mappings_investment_instruments_instrumen~",
                        column: x => x.instrument_id,
                        principalTable: "investment_instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "market_quote_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_symbol = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    exchange = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mic = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    price = table.Column<decimal>(type: "numeric(24,12)", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    price_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    as_of = table.Column<DateOnly>(type: "date", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_fallback = table.Column<bool>(type: "boolean", nullable: false),
                    raw_payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_quote_snapshots", x => x.id);
                    table.CheckConstraint("ck_market_quote_snapshots_price", "price > 0");
                    table.ForeignKey(
                        name: "FK_market_quote_snapshots_investment_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "investment_instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "investment_contribution_plan_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contribution_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: true),
                    instrument_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    native_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    current_value_eur = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    target_weight = table.Column<decimal>(type: "numeric(9,8)", nullable: false),
                    recommended_amount_eur = table.Column<decimal>(type: "numeric(20,8)", nullable: false),
                    recommended_native_amount = table.Column<decimal>(type: "numeric(20,8)", nullable: true),
                    suggested_quantity = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    allocation_score = table.Column<int>(type: "integer", nullable: true),
                    explanation = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    quote_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quote_as_of = table.Column<DateOnly>(type: "date", nullable: true),
                    fx_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fx_as_of = table.Column<DateOnly>(type: "date", nullable: true),
                    native_currency_per_eur = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    freshness = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_contribution_plan_lines", x => x.id);
                    table.CheckConstraint("ck_investment_contribution_plan_lines_amounts", "current_value_eur >= 0 AND recommended_amount_eur >= 0");
                    table.CheckConstraint("ck_investment_contribution_plan_lines_fx", "native_currency_per_eur IS NULL OR native_currency_per_eur > 0");
                    table.CheckConstraint("ck_investment_contribution_plan_lines_native_amount", "recommended_native_amount IS NULL OR recommended_native_amount >= 0");
                    table.CheckConstraint("ck_investment_contribution_plan_lines_price", "unit_price IS NULL OR unit_price > 0");
                    table.CheckConstraint("ck_investment_contribution_plan_lines_quantity", "suggested_quantity IS NULL OR suggested_quantity >= 0");
                    table.CheckConstraint("ck_investment_contribution_plan_lines_target", "target_weight >= 0 AND target_weight <= 1");
                    table.ForeignKey(
                        name: "FK_investment_contribution_plan_lines_investment_contribution_~",
                        column: x => x.contribution_plan_id,
                        principalTable: "investment_contribution_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_investment_contribution_plan_lines_investment_instruments_i~",
                        column: x => x.instrument_id,
                        principalTable: "investment_instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fx_rate_snapshots_base_currency_quote_currency_as_of",
                table: "fx_rate_snapshots",
                columns: new[] { "base_currency", "quote_currency", "as_of" });

            migrationBuilder.CreateIndex(
                name: "IX_fx_rate_snapshots_base_currency_quote_currency_as_of_provid~",
                table: "fx_rate_snapshots",
                columns: new[] { "base_currency", "quote_currency", "as_of", "provider_code" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_contribution_plan_lines_contribution_plan_id_ass~",
                table: "investment_contribution_plan_lines",
                columns: new[] { "contribution_plan_id", "asset_class" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_contribution_plan_lines_instrument_id",
                table: "investment_contribution_plan_lines",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "IX_investment_contribution_plans_confirmation_idempotency_key",
                table: "investment_contribution_plans",
                column: "confirmation_idempotency_key",
                unique: true,
                filter: "confirmation_idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_investment_contribution_plans_portfolio_id_created_at",
                table: "investment_contribution_plans",
                columns: new[] { "portfolio_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_market_instrument_mappings_instrument_id_provider_code",
                table: "market_instrument_mappings",
                columns: new[] { "instrument_id", "provider_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_market_instrument_mappings_provider_code_provider_symbol_ex~",
                table: "market_instrument_mappings",
                columns: new[] { "provider_code", "provider_symbol", "exchange" });

            migrationBuilder.CreateIndex(
                name: "IX_market_quote_snapshots_instrument_id_as_of",
                table: "market_quote_snapshots",
                columns: new[] { "instrument_id", "as_of" });

            migrationBuilder.CreateIndex(
                name: "IX_market_quote_snapshots_instrument_id_as_of_provider_code",
                table: "market_quote_snapshots",
                columns: new[] { "instrument_id", "as_of", "provider_code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fx_rate_snapshots");

            migrationBuilder.DropTable(
                name: "investment_contribution_plan_lines");

            migrationBuilder.DropTable(
                name: "market_instrument_mappings");

            migrationBuilder.DropTable(
                name: "market_quote_snapshots");

            migrationBuilder.DropTable(
                name: "investment_contribution_plans");
        }
    }
}
