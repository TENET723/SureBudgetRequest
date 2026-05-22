using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addAdvanceWithdraw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "reconciliation_deadline",
                table: "budget_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reconciliation_submitted_at",
                table: "budget_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refund_amount",
                table: "budget_requests",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "refund_received_at",
                table: "budget_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "refund_received_by_user_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "advance_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spent_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_advance_usages", x => x.id);
                    table.ForeignKey(
                        name: "fk_advance_usages_attachments_attachment_id",
                        column: x => x.attachment_id,
                        principalTable: "attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_advance_usages_budget_requests_budget_request_id",
                        column: x => x.budget_request_id,
                        principalTable: "budget_requests",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_advance_usages_attachment_id",
                table: "advance_usages",
                column: "attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_advance_usages_budget_request_id",
                table: "advance_usages",
                column: "budget_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "advance_usages");

            migrationBuilder.DropColumn(
                name: "reconciliation_deadline",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "reconciliation_submitted_at",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "refund_amount",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "refund_received_at",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "refund_received_by_user_id",
                table: "budget_requests");
        }
    }
}
