namespace Gma.Modules.Organizations.Persistence.Configurations;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class OrganizationEnrollmentClaimConfiguration
    : IEntityTypeConfiguration<OrganizationEnrollmentClaim>
{
    public void Configure(EntityTypeBuilder<OrganizationEnrollmentClaim> builder)
    {
        builder.ToTable("organization_enrollment_claims");
        builder.HasKey(claim => claim.Id);
        builder.Ignore(claim => claim.DomainEvents);
        builder.Property(claim => claim.SubjectId)
            .HasMaxLength(OrganizationSubjectId.MaxLength)
            .IsRequired();
        builder.Property(claim => claim.Status).HasConversion<int>();
        builder.Property(claim => claim.Version).IsConcurrencyToken();
        builder.Property(claim => claim.LastChangedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.HasIndex(claim => new { claim.EnrollmentLinkId, claim.SubjectId }).IsUnique();
        builder.HasIndex(claim => new { claim.OrganizationId, claim.Status, claim.CreatedAtUtc });
        builder.HasIndex(claim => new
        {
            claim.OrganizationId,
            claim.SubjectId,
            claim.Status,
            claim.DecisionExpiresAtUtc
        });
        builder.HasIndex(claim => new { claim.Status, claim.DecisionExpiresAtUtc });
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(claim => claim.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationEnrollmentLink>()
            .WithMany()
            .HasForeignKey(claim => claim.EnrollmentLinkId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationMembership>()
            .WithMany()
            .HasForeignKey(claim => claim.MembershipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
