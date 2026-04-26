using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    essential_expenses = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    saved_emergency_fund = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_profiles", x => x.id);
                    table.CheckConstraint("ck_health_profiles_essential_expenses", "essential_expenses >= 0");
                    table.CheckConstraint("ck_health_profiles_saved_emergency_fund", "saved_emergency_fund >= 0");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_profiles");
        }
    }
}
