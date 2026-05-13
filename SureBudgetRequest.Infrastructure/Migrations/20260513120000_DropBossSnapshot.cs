using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropBossSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Boss role becomes Management Team — multi-member. Any Management user can
            // approve, so the per-request snapshot of "the" boss is no longer meaningful.
            // The role check happens in the Application layer (mirrors Finance pattern).
            migrationBuilder.DropColumn(
                name: "boss_id_at_submission",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "boss_name_at_submission",
                table: "budget_requests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<System.Guid>(
                name: "boss_id_at_submission",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "boss_name_at_submission",
                table: "budget_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
