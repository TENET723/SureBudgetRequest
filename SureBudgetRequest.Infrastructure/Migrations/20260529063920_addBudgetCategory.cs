using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addBudgetCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "budget_category_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "budget_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_categories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_requests_budget_category_id",
                table: "budget_requests",
                column: "budget_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_categories_name",
                table: "budget_categories",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_budget_requests_budget_categories_budget_category_id",
                table: "budget_requests",
                column: "budget_category_id",
                principalTable: "budget_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_budget_requests_budget_categories_budget_category_id",
                table: "budget_requests");

            migrationBuilder.DropTable(
                name: "budget_categories");

            migrationBuilder.DropIndex(
                name: "ix_budget_requests_budget_category_id",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "budget_category_id",
                table: "budget_requests");
        }
    }
}
