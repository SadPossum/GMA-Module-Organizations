namespace Gma.Modules.Organizations.Persistence.Configurations;

using Gma.Modules.Organizations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LifecycleLimits =
    Gma.Modules.Organizations.Contracts.OrganizationScopeLifecycleLimits;

internal sealed class OrganizationScopeDestroyReceiptConfiguration
    : IEntityTypeConfiguration<OrganizationScopeDestroyReceipt>
{
    public void Configure(
        EntityTypeBuilder<OrganizationScopeDestroyReceipt> builder)
    {
        builder.ToTable(
            "organization_scope_destroy_receipts",
            table =>
            {
                table.HasTrigger(
                    "organization_scope_destroy_receipts_append_only");
                table.HasCheckConstraint(
                    "CK_organization_scope_destroy_receipts_revisions",
                    "\"ExpectedRevision\" >= 0 AND " +
                    "\"ResultingRevision\" > \"ExpectedRevision\"");
                table.HasCheckConstraint(
                    "CK_organization_scope_destroy_receipts_progress",
                    "\"BatchSize\" >= 1 AND \"BatchSize\" <= " +
                    LifecycleLimits.MaximumDestroyBatchSize +
                    " AND \"RemovedRecordCount\" >= 0 AND " +
                    "\"CompletedBatchCount\" >= 0 AND " +
                    "\"RemovalProofVersion\" >= 1 AND " +
                    "\"CompletedAtUtc\" >= \"StartedAtUtc\"");
            });
        builder.HasKey(receipt => receipt.OrganizationId);
        builder.HasIndex(receipt => receipt.OperationId).IsUnique();
        builder.Property(receipt => receipt.RequestSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.RemovalProofSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.HasOne<OrganizationScopeState>()
            .WithMany()
            .HasForeignKey(receipt => receipt.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
