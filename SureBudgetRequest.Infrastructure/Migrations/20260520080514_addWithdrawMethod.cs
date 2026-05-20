using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addWithdrawMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "withdraw_method_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "withdraw_methods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_withdraw_methods", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_requests_withdraw_method_id",
                table: "budget_requests",
                column: "withdraw_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_withdraw_methods_name",
                table: "withdraw_methods",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_budget_requests_withdraw_methods_withdraw_method_id",
                table: "budget_requests",
                column: "withdraw_method_id",
                principalTable: "withdraw_methods",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_budget_requests_withdraw_methods_withdraw_method_id",
                table: "budget_requests");

            migrationBuilder.DropTable(
                name: "withdraw_methods");

            migrationBuilder.DropIndex(
                name: "ix_budget_requests_withdraw_method_id",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "withdraw_method_id",
                table: "budget_requests");
        }
    }
}
