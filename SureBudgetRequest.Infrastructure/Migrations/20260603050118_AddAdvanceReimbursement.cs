using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvanceReimbursement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "reimbursement_amount",
                table: "budget_requests",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "reimbursement_paid_at",
                table: "budget_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reimbursement_paid_by_user_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reimbursement_amount",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "reimbursement_paid_at",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "reimbursement_paid_by_user_id",
                table: "budget_requests");
        }
    }
}
