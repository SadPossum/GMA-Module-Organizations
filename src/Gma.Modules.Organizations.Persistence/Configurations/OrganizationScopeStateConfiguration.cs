namespace Gma.Modules.Organizations.Persistence.Configurations;

using Gma.Modules.Organizations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class OrganizationScopeStateConfiguration
    : IEntityTypeConfiguration<OrganizationScopeState>
{
    public void Configure(EntityTypeBuilder<OrganizationScopeState> builder)
    {
        builder.ToTable(
            "organization_scope_states",
            table =>
            {
                table.HasTrigger(
                    "organization_scope_states_closed_immutable");
                table.HasCheckConstraint(
                    "CK_organization_scope_states_version",
                    "\"Version\" >= 0");
                table.HasCheckConstraint(
                    "CK_organization_scope_states_closure",
                    "(CAST(\"IsClosed\" AS integer) = 0 AND " +
                    "\"CloseOperationId\" IS NULL " +
                    "AND \"CloseRequestSha256\" IS NULL AND " +
                    "\"ClosedAtUtc\" IS NULL) OR " +
                    "(CAST(\"IsClosed\" AS integer) = 1 AND " +
                    "\"CloseOperationId\" IS NOT NULL " +
                    "AND \"CloseRequestSha256\" IS NOT NULL AND " +
                    "\"ClosedAtUtc\" IS NOT NULL)");
            });
        builder.HasKey(state => state.OrganizationId);
        builder.Property(state => state.ScopeId)
            .HasMaxLength(OrganizationScopeState.ScopeIdLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(state => state.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(state => state.CloseRequestSha256)
            .HasMaxLength(64)
            .IsFixedLength();
        builder.HasIndex(state => state.ScopeId).IsUnique();
        builder.HasIndex(state => new
        {
            state.IsClosed,
            state.ClosedAtUtc,
            state.OrganizationId
        });
    }
}
