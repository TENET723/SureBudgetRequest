using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorToBudgetRequestAndPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "vendor_payment_account_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vendor_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vendor_payment_account_id",
                table: "budget_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_vendor_payment_account_id",
                table: "payments",
                column: "vendor_payment_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_requests_vendor_id",
                table: "budget_requests",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_requests_vendor_payment_account_id",
                table: "budget_requests",
                column: "vendor_payment_account_id");

            migrationBuilder.AddForeignKey(
                name: "fk_budget_requests_vendor_payment_accounts_vendor_payment_acco",
                table: "budget_requests",
                column: "vendor_payment_account_id",
                principalTable: "vendor_payment_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_budget_requests_vendors_vendor_id",
                table: "budget_requests",
                column: "vendor_id",
                principalTable: "vendors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_payments_vendor_payment_accounts_vendor_payment_account_id",
                table: "payments",
                column: "vendor_payment_account_id",
                principalTable: "vendor_payment_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_budget_requests_vendor_payment_accounts_vendor_payment_acco",
                table: "budget_requests");

            migrationBuilder.DropForeignKey(
                name: "fk_budget_requests_vendors_vendor_id",
                table: "budget_requests");

            migrationBuilder.DropForeignKey(
                name: "fk_payments_vendor_payment_accounts_vendor_payment_account_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_vendor_payment_account_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_budget_requests_vendor_id",
                table: "budget_requests");

            migrationBuilder.DropIndex(
                name: "ix_budget_requests_vendor_payment_account_id",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "vendor_payment_account_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "vendor_id",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "vendor_payment_account_id",
                table: "budget_requests");
        }
    }
}
