using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addMonthlyBudgetLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "monthly_limit",
                table: "departments",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_limit_at_submission",
                table: "budget_requests",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "monthly_overrun_justification",
                table: "budget_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_spend_before_at_submission",
                table: "budget_requests",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_budget_requests_department_id_at_submission_status_submitte",
                table: "budget_requests",
                columns: new[] { "department_id_at_submission", "status", "submitted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_budget_requests_department_id_at_submission_status_submitte",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "monthly_limit",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "monthly_limit_at_submission",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "monthly_overrun_justification",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "monthly_spend_before_at_submission",
                table: "budget_requests");
        }
    }
}
