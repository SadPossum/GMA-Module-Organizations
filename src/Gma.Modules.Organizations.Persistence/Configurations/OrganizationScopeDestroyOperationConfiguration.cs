namespace Gma.Modules.Organizations.Persistence.Configurations;

using Gma.Modules.Organizations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LifecycleLimits =
    Gma.Modules.Organizations.Application.Ports.OrganizationScopeLifecycleLimits;

internal sealed class OrganizationScopeDestroyOperationConfiguration
    : IEntityTypeConfiguration<OrganizationScopeDestroyOperation>
{
    public void Configure(
        EntityTypeBuilder<OrganizationScopeDestroyOperation> builder)
    {
        builder.ToTable(
            "organization_scope_destroy_operations",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_organization_scope_destroy_operations_revisions",
                    "\"ExpectedRevision\" >= 0 AND " +
                    "\"ResultingRevision\" > \"ExpectedRevision\"");
                table.HasCheckConstraint(
                    "CK_organization_scope_destroy_operations_batch",
                    "\"BatchSize\" >= 1 AND \"BatchSize\" <= " +
                    LifecycleLimits.MaximumDestroyBatchSize);
                table.HasCheckConstraint(
                    "CK_organization_scope_destroy_operations_progress",
                    "\"Stage\" >= 1 AND \"Stage\" <= 7 AND " +
                    "\"RemovedRecordCount\" >= 0 AND " +
                    "\"CompletedBatchCount\" >= 0 AND " +
                    "\"ProofVersion\" >= 1 AND " +
                    "\"UpdatedAtUtc\" >= \"StartedAtUtc\"");
            });
        builder.HasKey(operation => operation.OrganizationId);
        builder.HasIndex(operation => operation.OperationId).IsUnique();
        builder.Property(operation => operation.RequestSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(operation => operation.Stage).HasConversion<int>();
        builder.Property(operation => operation.RemovalProofSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(operation => operation.UpdatedAtUtc)
            .IsConcurrencyToken();
        builder.HasOne<OrganizationScopeState>()
            .WithMany()
            .HasForeignKey(operation => operation.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
