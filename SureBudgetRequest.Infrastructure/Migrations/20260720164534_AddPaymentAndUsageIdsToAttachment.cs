using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAndUsageIdsToAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "advance_usage_id",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "payment_id",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE attachments 
                SET payment_id = payments.id 
                FROM payments 
                WHERE payments.attachment_id = attachments.id;
            ");

            migrationBuilder.Sql(@"
                UPDATE attachments 
                SET advance_usage_id = advance_usages.id 
                FROM advance_usages 
                WHERE advance_usages.attachment_id = attachments.id;
            ");

            migrationBuilder.DropForeignKey(
                name: "fk_advance_usages_attachments_attachment_id",
                table: "advance_usages");

            migrationBuilder.DropIndex(
                name: "ix_advance_usages_attachment_id",
                table: "advance_usages");

            migrationBuilder.DropColumn(
                name: "attachment_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "attachment_id",
                table: "advance_usages");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_advance_usage_id",
                table: "attachments",
                column: "advance_usage_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_payment_id",
                table: "attachments",
                column: "payment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_advance_usages_advance_usage_id",
                table: "attachments",
                column: "advance_usage_id",
                principalTable: "advance_usages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_payments_payment_id",
                table: "attachments",
                column: "payment_id",
                principalTable: "payments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_advance_usages_advance_usage_id",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_attachments_payments_payment_id",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "ix_attachments_advance_usage_id",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "ix_attachments_payment_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "advance_usage_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "payment_id",
                table: "attachments");

            migrationBuilder.AddColumn<Guid>(
                name: "attachment_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "attachment_id",
                table: "advance_usages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_advance_usages_attachment_id",
                table: "advance_usages",
                column: "attachment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_advance_usages_attachments_attachment_id",
                table: "advance_usages",
                column: "attachment_id",
                principalTable: "attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
