using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planning_goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    saved_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    months = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planning_goals", x => x.id);
                    table.CheckConstraint("ck_planning_goals_months_positive", "months >= 1");
                    table.CheckConstraint("ck_planning_goals_saved_amount_non_negative", "saved_amount >= 0");
                    table.CheckConstraint("ck_planning_goals_total_amount_non_negative", "total_amount >= 0");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planning_goals");
        }
    }
}
