namespace Gma.Modules.Organizations.Persistence.Configurations;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(organization => organization.Id);
        builder.Ignore(organization => organization.ScopeId);
        builder.Ignore(organization => organization.DomainEvents);
        builder.Property(organization => organization.Name)
            .HasMaxLength(OrganizationName.MaxLength)
            .IsRequired();
        builder.Property(organization => organization.Slug)
            .HasMaxLength(OrganizationSlug.MaxLength)
            .IsRequired();
        builder.Property(organization => organization.CreationRequestFingerprint)
            .HasMaxLength(64)
            .IsFixedLength();
        builder.HasIndex(organization => organization.Slug).IsUnique();
        builder.Property(organization => organization.Status).HasConversion<int>();
        builder.Property(organization => organization.Version).IsConcurrencyToken();
        builder.Property(organization => organization.CreatedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.Property(organization => organization.LastChangedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.Property(organization => organization.LastMutationKind)
            .HasConversion<int?>();
        builder.HasIndex(organization => new { organization.Status, organization.Slug });
    }
}
