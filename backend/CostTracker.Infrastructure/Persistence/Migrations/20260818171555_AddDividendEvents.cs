using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDividendEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dividend_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gross_amount_per_unit = table.Column<decimal>(type: "numeric(24,12)", nullable: false),
                    withholding_tax_rate = table.Column<decimal>(type: "numeric(9,8)", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    ex_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    eligible_quantity = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    gross_amount = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    withholding_tax_amount = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    net_amount = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    currency_per_eur_rate = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    net_amount_eur = table.Column<decimal>(type: "numeric(24,12)", nullable: true),
                    fx_as_of = table.Column<DateOnly>(type: "date", nullable: true),
                    fx_provider_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dividend_events", x => x.id);
                    table.CheckConstraint("ck_dividend_events_amount_per_unit_positive", "gross_amount_per_unit > 0");
                    table.CheckConstraint("ck_dividend_events_amounts", "(gross_amount IS NULL OR gross_amount >= 0) AND (withholding_tax_amount IS NULL OR withholding_tax_amount >= 0) AND (net_amount IS NULL OR net_amount >= 0)");
                    table.CheckConstraint("ck_dividend_events_eligible_quantity", "eligible_quantity IS NULL OR eligible_quantity >= 0");
                    table.CheckConstraint("ck_dividend_events_payment_after_ex", "payment_date >= ex_date");
                    table.CheckConstraint("ck_dividend_events_tax_rate", "withholding_tax_rate >= 0 AND withholding_tax_rate < 1");
                    table.ForeignKey(
                        name: "FK_dividend_events_investment_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "investment_instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dividend_events_idempotency_key",
                table: "dividend_events",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dividend_events_instrument_id_ex_date_payment_date",
                table: "dividend_events",
                columns: new[] { "instrument_id", "ex_date", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "IX_dividend_events_processed_at_payment_date",
                table: "dividend_events",
                columns: new[] { "processed_at", "payment_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dividend_events");
        }
    }
}
