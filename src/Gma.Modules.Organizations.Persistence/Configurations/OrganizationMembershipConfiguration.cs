namespace Gma.Modules.Organizations.Persistence.Configurations;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships");
        builder.HasKey(membership => membership.Id);
        builder.Ignore(membership => membership.DomainEvents);
        builder.Property(membership => membership.SubjectId)
            .HasMaxLength(OrganizationSubjectId.MaxLength)
            .IsRequired();
        builder.Property(membership => membership.Role).HasConversion<int>();
        builder.Property(membership => membership.Status).HasConversion<int>();
        builder.Property(membership => membership.Version).IsConcurrencyToken();
        builder.Property(membership => membership.CreatedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.Property(membership => membership.LastChangedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.HasIndex(membership => new { membership.OrganizationId, membership.SubjectId }).IsUnique();
        builder.HasIndex(membership => new { membership.SubjectId, membership.Status, membership.OrganizationId });
        builder.HasIndex(membership => new { membership.OrganizationId, membership.Status, membership.Role });
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
