using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFallbackUserIdToLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fallback_user_id",
                table: "budget_request_modifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fallback_user_id",
                table: "approval_actions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_budget_request_modifications_fallback_user_id",
                table: "budget_request_modifications",
                column: "fallback_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_actions_fallback_user_id",
                table: "approval_actions",
                column: "fallback_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_approval_actions_users_fallback_user_id",
                table: "approval_actions",
                column: "fallback_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_budget_request_modifications_users_fallback_user_id",
                table: "budget_request_modifications",
                column: "fallback_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_approval_actions_users_fallback_user_id",
                table: "approval_actions");

            migrationBuilder.DropForeignKey(
                name: "fk_budget_request_modifications_users_fallback_user_id",
                table: "budget_request_modifications");

            migrationBuilder.DropIndex(
                name: "ix_budget_request_modifications_fallback_user_id",
                table: "budget_request_modifications");

            migrationBuilder.DropIndex(
                name: "ix_approval_actions_fallback_user_id",
                table: "approval_actions");

            migrationBuilder.DropColumn(
                name: "fallback_user_id",
                table: "budget_request_modifications");

            migrationBuilder.DropColumn(
                name: "fallback_user_id",
                table: "approval_actions");
        }
    }
}
