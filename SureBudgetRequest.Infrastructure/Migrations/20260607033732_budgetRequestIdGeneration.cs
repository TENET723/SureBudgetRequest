using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class budgetRequestIdGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "budget_request_ref_seq");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "budget_request_ref_seq");
        }
    }
}
