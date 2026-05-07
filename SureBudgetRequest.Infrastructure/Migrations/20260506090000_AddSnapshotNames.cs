using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotNames : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
