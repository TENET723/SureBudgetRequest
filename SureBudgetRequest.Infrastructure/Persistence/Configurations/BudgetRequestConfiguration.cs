using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class BudgetRequestConfiguration : IEntityTypeConfiguration<BudgetRequest>
{
    public void Configure(EntityTypeBuilder<BudgetRequest> builder)
    {
        builder.ToTable("budget_requests");

        builder.HasKey(r => r.Id);

        // ── Requester fields ──────────────────────────────────────────────────
        builder.Property(r => r.RequesterId).IsRequired();
        builder.Property(r => r.RequestDate).IsRequired();
        builder.Property(r => r.RequestedAmount)
               .IsRequired()
               .HasColumnType("numeric(18,2)");
        builder.Property(r => r.Reasons).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.WithdrawerName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.WithdrawerJobTitle).IsRequired().HasMaxLength(200);
        builder.Property(r => r.AllowsPartialPayment).IsRequired();
        builder.Property(r => r.PartialPaymentDetail).HasMaxLength(1000);

        // Nullable — only populated when the dept has a monthly limit AND the
        // requested amount would push them over. Validated by Submit().
        builder.Property(r => r.MonthlyOverrunJustification).HasMaxLength(2000);

        // ── Currency (draft-phase, editable while Draft/SentBack) ─────────────
        builder.Property(r => r.CurrencyCode).IsRequired().HasMaxLength(10);
        builder.HasIndex(r => r.CurrencyCode);
        builder.HasOne<Currency>()
               .WithMany()
               .HasForeignKey(r => r.CurrencyCode)
               .HasPrincipalKey(c => c.Code)
               .OnDelete(DeleteBehavior.Restrict);

        // ── Submission snapshots ──────────────────────────────────────────────
        builder.Property(r => r.DepartmentIdAtSubmission).IsRequired();
        builder.Property(r => r.DepartmentLimitAtSubmission)
               .IsRequired()
               .HasColumnType("numeric(18,2)");

        // Both nullable — null when the dept had no monthly limit at submission.
        builder.Property(r => r.MonthlyLimitAtSubmission)
               .HasColumnType("numeric(18,2)");
        builder.Property(r => r.MonthlySpendBeforeAtSubmission)
               .HasColumnType("numeric(18,2)");

        builder.Property(r => r.ExchangeRateAtSubmission)
               .IsRequired()
               .HasColumnType("numeric(18,6)");
        builder.Property(r => r.RequestedAmountInMmkAtSubmission)
               .IsRequired()
               .HasColumnType("numeric(18,2)");
        builder.Property(r => r.DeptHeadIdAtSubmission).IsRequired();
        builder.Property(r => r.DeptHeadNameAtSubmission).IsRequired().HasMaxLength(200);
        builder.Property(r => r.RequesterNameAtSubmission).IsRequired().HasMaxLength(200);

        // ── Workflow state ────────────────────────────────────────────────────
        builder.Property(r => r.Type)
               .IsRequired()
               .HasConversion<int>();
        builder.Property(r => r.Status)
               .IsRequired()
               .HasConversion<int>();  // store as int, not PG enum (easier to evolve)

        builder.Property(r => r.ApprovedAmount)
               .IsRequired()
               .HasColumnType("numeric(18,2)");

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.SubmittedAt);
        builder.Property(r => r.FinalizedAt);

        // ── Advance withdrawal — reconciliation phase ─────────────────────────
        // All nullable except RefundAmount, which defaults to 0 for every
        // non-advance request (and for advances that reconciled exactly).
        builder.Property(r => r.ReconciliationDeadline);
        builder.Property(r => r.ReconciliationSubmittedAt);
        builder.Property(r => r.RefundAmount)
               .IsRequired()
               .HasColumnType("numeric(18,2)");
        builder.Property(r => r.RefundReceivedAt);
        builder.Property(r => r.RefundReceivedByUserId);

        // ── Chart of Account FK ───────────────────────────────────────────────
        // Nullable — only set when Finance has approved (and stays set through
        // PartiallyPaid / Paid / SentBack). Restrict on delete: a Coa cannot be
        // deleted while any request references it. Admins should Deactivate
        // unused codes instead of deleting them.
        builder.Property(r => r.CoaId);
        builder.HasOne<Coa>()
               .WithMany()
               .HasForeignKey(r => r.CoaId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.CoaId);

        // ── Withdraw Method FK ────────────────────────────────────────────────
        // Nullable in DB only for rows that pre-date the feature; new drafts
        // require it. Restrict on delete so a method cannot be hard-deleted
        // while referenced — admins must Deactivate instead.
        builder.Property(r => r.WithdrawMethodId);
        builder.HasOne<WithdrawMethod>()
               .WithMany()
               .HasForeignKey(r => r.WithdrawMethodId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.WithdrawMethodId);

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(r => r.RequesterId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.DepartmentIdAtSubmission);

        // Composite index supporting the monthly-spend aggregate query
        // (filter by dept + status + SubmittedAt range).
        builder.HasIndex(r => new { r.DepartmentIdAtSubmission, r.Status, r.SubmittedAt });

        // ── Private collection backing fields ─────────────────────────────────
        // EF Core must use the private List<T> fields, not the IReadOnlyList<T> properties.

        builder.HasMany(r => r.ApprovalActions)
               .WithOne()
               .HasForeignKey(a => a.BudgetRequestId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.ApprovalActions)
               .HasField("_approvalActions")
               .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.Payments)
               .WithOne()
               .HasForeignKey(p => p.BudgetRequestId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Payments)
               .HasField("_payments")
               .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.Attachments)
               .WithOne()
               .HasForeignKey(a => a.BudgetRequestId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Attachments)
               .HasField("_attachments")
               .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Advance-usage line items. ClientCascade — no DB-level ON DELETE
        // CASCADE; EF deletes orphans client-side when the aggregate removes a
        // usage from the collection (RemoveAdvanceUsage). The aggregate owns the
        // lifecycle, mirroring the task's "aggregate manages it" requirement.
        builder.HasMany(r => r.AdvanceUsages)
               .WithOne()
               .HasForeignKey(u => u.BudgetRequestId)
               .OnDelete(DeleteBehavior.ClientCascade);

        builder.Navigation(r => r.AdvanceUsages)
               .HasField("_advanceUsages")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
