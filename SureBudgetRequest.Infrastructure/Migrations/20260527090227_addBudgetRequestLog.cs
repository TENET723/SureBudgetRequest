using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addBudgetRequestLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "budget_request_modifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modified_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_request_modifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_budget_request_modifications_budget_requests_budget_request",
                        column: x => x.budget_request_id,
                        principalTable: "budget_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_budget_request_modifications_users_modified_by_user_id",
                        column: x => x.modified_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_request_modifications_budget_request_id",
                table: "budget_request_modifications",
                column: "budget_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_request_modifications_modified_at",
                table: "budget_request_modifications",
                column: "modified_at");

            migrationBuilder.CreateIndex(
                name: "ix_budget_request_modifications_modified_by_user_id",
                table: "budget_request_modifications",
                column: "modified_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_request_modifications");
        }
    }
}
