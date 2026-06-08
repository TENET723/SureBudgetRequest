using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetRequestRefSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RestartSequence(
                name: "budget_request_ref_seq",
                startValue: 10000L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RestartSequence(
                name: "budget_request_ref_seq",
                startValue: 1L);
        }
    }
}
