using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingCategoryAndPrintLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "accounting_category_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "first_printed_at",
                table: "budget_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "first_printed_by_user_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accounting_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounting_categories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_requests_accounting_category_id",
                table: "budget_requests",
                column: "accounting_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounting_categories_name",
                table: "accounting_categories",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_budget_requests_accounting_categories_accounting_category_id",
                table: "budget_requests",
                column: "accounting_category_id",
                principalTable: "accounting_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_budget_requests_accounting_categories_accounting_category_id",
                table: "budget_requests");

            migrationBuilder.DropTable(
                name: "accounting_categories");

            migrationBuilder.DropIndex(
                name: "ix_budget_requests_accounting_category_id",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "accounting_category_id",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "first_printed_at",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "first_printed_by_user_id",
                table: "budget_requests");
        }
    }
}
