using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addDepartMentBudgetPeriodTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "monthly_limit",
                table: "departments");

            migrationBuilder.RenameColumn(
                name: "monthly_spend_before_at_submission",
                table: "budget_requests",
                newName: "period_spend_before_at_submission");

            migrationBuilder.RenameColumn(
                name: "monthly_limit_at_submission",
                table: "budget_requests",
                newName: "period_limit_at_submission");

            migrationBuilder.AddColumn<DateTime>(
                name: "period_end_at_submission",
                table: "budget_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "period_start_at_submission",
                table: "budget_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "period_type_at_submission",
                table: "budget_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "department_budget_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from_financial_year = table.Column<int>(type: "integer", nullable: false),
                    period_type = table.Column<int>(type: "integer", nullable: false),
                    limit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_department_budget_periods", x => x.id);
                    table.ForeignKey(
                        name: "fk_department_budget_periods_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_department_budget_periods_department_id_effective_from_fina",
                table: "department_budget_periods",
                columns: new[] { "department_id", "effective_from_financial_year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "department_budget_periods");

            migrationBuilder.DropColumn(
                name: "period_end_at_submission",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "period_start_at_submission",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "period_type_at_submission",
                table: "budget_requests");

            migrationBuilder.RenameColumn(
                name: "period_spend_before_at_submission",
                table: "budget_requests",
                newName: "monthly_spend_before_at_submission");

            migrationBuilder.RenameColumn(
                name: "period_limit_at_submission",
                table: "budget_requests",
                newName: "monthly_limit_at_submission");

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_limit",
                table: "departments",
                type: "numeric(18,2)",
                nullable: true);
        }
    }
}
