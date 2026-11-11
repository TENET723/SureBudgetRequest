using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorAndVendorPaymentAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vendors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_person = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vendor_payment_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_type = table.Column<int>(type: "integer", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_holder_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    branch_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attachment_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    attachment_stored_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    attachment_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attachment_size_bytes = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_payment_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_payment_accounts_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vendor_payment_accounts_vendor_id",
                table: "vendor_payment_accounts",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendors_name",
                table: "vendors",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vendor_payment_accounts");

            migrationBuilder.DropTable(
                name: "vendors");
        }
    }
}
