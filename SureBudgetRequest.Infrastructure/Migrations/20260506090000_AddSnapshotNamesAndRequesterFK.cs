using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotNamesAndRequesterFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── New name snapshot columns ─────────────────────────────────────

            migrationBuilder.AddColumn<string>(
                name: "requester_name_at_submission",
                table: "budget_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "dept_head_name_at_submission",
                table: "budget_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "boss_name_at_submission",
                table: "budget_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // ── FK: budget_requests.requester_id → users.id ───────────────────
            // RESTRICT: prevents deleting a user who has any budget request.
            // Deactivate the user instead (IsActive = false).

            migrationBuilder.AddForeignKey(
                name: "fk_budget_requests_users_requester_id",
                table: "budget_requests",
                column: "requester_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_budget_requests_users_requester_id",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "requester_name_at_submission",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "dept_head_name_at_submission",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "boss_name_at_submission",
                table: "budget_requests");
        }
    }
}
