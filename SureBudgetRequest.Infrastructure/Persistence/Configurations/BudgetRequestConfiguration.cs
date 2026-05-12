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
        builder.Property(r => r.ExchangeRateAtSubmission)
               .IsRequired()
               .HasColumnType("numeric(18,6)");
        builder.Property(r => r.RequestedAmountInMmkAtSubmission)
               .IsRequired()
               .HasColumnType("numeric(18,2)");
        builder.Property(r => r.DeptHeadIdAtSubmission).IsRequired();
        builder.Property(r => r.DeptHeadNameAtSubmission).IsRequired().HasMaxLength(200);
        builder.Property(r => r.BossIdAtSubmission);        // nullable
        builder.Property(r => r.BossNameAtSubmission).HasMaxLength(200);
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

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(r => r.RequesterId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.DepartmentIdAtSubmission);

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
    }
}
