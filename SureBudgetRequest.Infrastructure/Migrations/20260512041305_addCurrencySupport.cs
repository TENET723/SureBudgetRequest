using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SureBudgetRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addCurrencySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. currencies table ──────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rate_to_mmk = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    rate_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currencies", x => x.code);
                });

            // ── 2. Seed MMK BEFORE any FK references it ──────────────────────
            //   Required for the FK on budget_requests.currency_code to succeed,
            //   and for the FK on currency_rate_changes.currency_code below.
            var seedTime = DateTime.UtcNow;
            migrationBuilder.InsertData(
                table: "currencies",
                columns: new[] { "code", "name", "rate_to_mmk", "is_active", "rate_updated_at", "created_at" },
                values: new object[] { "MMK", "Myanmar Kyat", 1.0m, true, seedTime, seedTime });

            // ── 3. currency_rate_changes audit table ─────────────────────────
            migrationBuilder.CreateTable(
                name: "currency_rate_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    old_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    new_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currency_rate_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_currency_rate_changes_currencies_currency_code",
                        column: x => x.currency_code,
                        principalTable: "currencies",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_currency_rate_changes_currency_code",
                table: "currency_rate_changes",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "ix_currency_rate_changes_changed_at",
                table: "currency_rate_changes",
                column: "changed_at");

            // ── 4. Add new columns to budget_requests ────────────────────────
            //   Defaults so existing rows are valid: every existing request is
            //   treated as MMK with rate 1.0, and its MMK-equivalent equals the
            //   original requested amount.
            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "budget_requests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "MMK");

            migrationBuilder.AddColumn<decimal>(
                name: "exchange_rate_at_submission",
                table: "budget_requests",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 1.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "requested_amount_in_mmk_at_submission",
                table: "budget_requests",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill MMK-equivalent for existing rows (= requested_amount since rate is 1.0)
            migrationBuilder.Sql(
                "UPDATE budget_requests SET requested_amount_in_mmk_at_submission = requested_amount;");

            // ── 5. Add FK + index from budget_requests.currency_code ─────────
            //   MMK already exists in currencies (step 2), so this succeeds.
            migrationBuilder.CreateIndex(
                name: "ix_budget_requests_currency_code",
                table: "budget_requests",
                column: "currency_code");

            migrationBuilder.AddForeignKey(
                name: "fk_budget_requests_currencies_currency_code",
                table: "budget_requests",
                column: "currency_code",
                principalTable: "currencies",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_budget_requests_currencies_currency_code",
                table: "budget_requests");

            migrationBuilder.DropIndex(
                name: "ix_budget_requests_currency_code",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "exchange_rate_at_submission",
                table: "budget_requests");

            migrationBuilder.DropColumn(
                name: "requested_amount_in_mmk_at_submission",
                table: "budget_requests");

            migrationBuilder.DropTable(
                name: "currency_rate_changes");

            migrationBuilder.DropTable(
                name: "currencies");
        }
    }
}
