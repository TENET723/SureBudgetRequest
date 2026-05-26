using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addPaymentAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "attachment_id",
                table: "payments",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attachment_id",
                table: "payments");
        }
    }
}
