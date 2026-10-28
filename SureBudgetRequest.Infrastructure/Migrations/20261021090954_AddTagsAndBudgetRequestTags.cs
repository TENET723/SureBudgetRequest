using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTagsAndBudgetRequestTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    color_hex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                    table.ForeignKey(
                        name: "fk_tags_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tags_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_request_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attached_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attached_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_request_tags", x => x.id);
                    table.ForeignKey(
                        name: "fk_budget_request_tags_budget_requests_budget_request_id",
                        column: x => x.budget_request_id,
                        principalTable: "budget_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_budget_request_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_budget_request_tags_users_attached_by_user_id",
                        column: x => x.attached_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_request_tags_attached_by_user_id",
                table: "budget_request_tags",
                column: "attached_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_request_tags_budget_request_id_tag_department_id",
                table: "budget_request_tags",
                columns: new[] { "budget_request_id", "tag_department_id" });

            migrationBuilder.CreateIndex(
                name: "ix_budget_request_tags_budget_request_id_tag_id",
                table: "budget_request_tags",
                columns: new[] { "budget_request_id", "tag_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_budget_request_tags_tag_id",
                table: "budget_request_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_tags_created_by_user_id",
                table: "tags",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tags_department_id_name",
                table: "tags",
                columns: new[] { "department_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_request_tags");

            migrationBuilder.DropTable(
                name: "tags");
        }
    }
}
