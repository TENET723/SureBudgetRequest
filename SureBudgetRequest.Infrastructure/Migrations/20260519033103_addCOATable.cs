using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addCOATable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "coa_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "coas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_requests_coa_id",
                table: "budget_requests",
                column: "coa_id");

            migrationBuilder.CreateIndex(
                name: "ix_coas_code",
                table: "coas",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_budget_requests_coas_coa_id",
                table: "budget_requests",
                column: "coa_id",
                principalTable: "coas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_budget_requests_coas_coa_id",
                table: "budget_requests");

            migrationBuilder.DropTable(
                name: "coas");

            migrationBuilder.DropIndex(
                name: "ix_budget_requests_coa_id",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "coa_id",
                table: "budget_requests");
        }
    }
}
