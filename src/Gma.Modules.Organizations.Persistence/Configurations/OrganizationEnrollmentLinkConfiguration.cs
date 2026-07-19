namespace Gma.Modules.Organizations.Persistence.Configurations;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class OrganizationEnrollmentLinkConfiguration
    : IEntityTypeConfiguration<OrganizationEnrollmentLink>
{
    public void Configure(EntityTypeBuilder<OrganizationEnrollmentLink> builder)
    {
        builder.ToTable("organization_enrollment_links");
        builder.HasKey(link => link.Id);
        builder.Ignore(link => link.DomainEvents);
        builder.Property(link => link.CreatorSubjectId)
            .HasMaxLength(OrganizationSubjectId.MaxLength)
            .IsRequired();
        builder.Property(link => link.TokenDigest)
            .HasMaxLength(OrganizationEnrollmentLink.TokenDigestLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(link => link.ApprovalMode).HasConversion<int>();
        builder.Property(link => link.Status).HasConversion<int>();
        builder.Property(link => link.Version).IsConcurrencyToken();
        builder.Property(link => link.CreatedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.Property(link => link.LastChangedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.HasIndex(link => link.TokenDigest).IsUnique();
        builder.HasIndex(link => new { link.OrganizationId, link.Status, link.CreatedAtUtc });
        builder.HasIndex(link => new { link.Status, link.ExpiresAtUtc });
        builder.HasIndex(link => new { link.Status, link.LastChangedAtUtc });
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(link => link.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
