using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addRefColToBudgetRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reference",
                table: "budget_requests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reference",
                table: "budget_requests");
        }
    }
}
