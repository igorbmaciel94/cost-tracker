using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "months",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    salary = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cloned_from_month_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_months", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "category_budgets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    month_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    group_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    planned_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_budgets", x => x.id);
                    table.CheckConstraint("ck_category_budgets_planned_amount_non_negative", "planned_amount >= 0");
                    table.ForeignKey(
                        name: "FK_category_budgets_months_month_id",
                        column: x => x.month_id,
                        principalTable: "months",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    month_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_percent = table.Column<decimal>(type: "numeric(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_targets_months_month_id",
                        column: x => x.month_id,
                        principalTable: "months",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    month_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_budget_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entries", x => x.id);
                    table.CheckConstraint("ck_entries_amount_non_negative", "amount >= 0");
                    table.ForeignKey(
                        name: "FK_entries_category_budgets_category_budget_id",
                        column: x => x.category_budget_id,
                        principalTable: "category_budgets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entries_months_month_id",
                        column: x => x.month_id,
                        principalTable: "months",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_category_budgets_month_id_name",
                table: "category_budgets",
                columns: new[] { "month_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entries_category_budget_id",
                table: "entries",
                column: "category_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_entries_month_id_entry_date",
                table: "entries",
                columns: new[] { "month_id", "entry_date" });

            migrationBuilder.CreateIndex(
                name: "IX_group_targets_month_id_group_name",
                table: "group_targets",
                columns: new[] { "month_id", "group_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_months_reference_month",
                table: "months",
                column: "reference_month",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_months_status",
                table: "months",
                column: "status",
                unique: true,
                filter: "\"status\" = 'OPEN'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entries");

            migrationBuilder.DropTable(
                name: "group_targets");

            migrationBuilder.DropTable(
                name: "category_budgets");

            migrationBuilder.DropTable(
                name: "months");
        }
    }
}
