namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.Organizations.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal static class OrganizationsOrdinalIdentityModel
{
    public static void Apply(
        ModelBuilder modelBuilder,
        OrganizationsDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(dbContext);

        modelBuilder.Entity<Organization>()
            .Property(organization => organization.CreatedBy)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<Organization>()
            .Property(organization => organization.LastChangedBy)
            .UseOrdinalStringComparison(dbContext);

        modelBuilder.Entity<OrganizationMembership>()
            .Property(membership => membership.SubjectId)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<OrganizationMembership>()
            .Property(membership => membership.CreatedBy)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<OrganizationMembership>()
            .Property(membership => membership.LastChangedBy)
            .UseOrdinalStringComparison(dbContext);

        modelBuilder.Entity<OrganizationInvitation>()
            .Property(invitation => invitation.InviterSubjectId)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<OrganizationInvitation>()
            .Property(invitation => invitation.AcceptedSubjectId)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<OrganizationInvitation>()
            .Property(invitation => invitation.CreatedBy)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<OrganizationInvitation>()
            .Property(invitation => invitation.LastChangedBy)
            .UseOrdinalStringComparison(dbContext);

        modelBuilder.Entity<OrganizationEnrollmentLink>()
            .Property(link => link.CreatorSubjectId)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<OrganizationEnrollmentLink>()
            .Property(link => link.CreatedBy)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<OrganizationEnrollmentLink>()
            .Property(link => link.LastChangedBy)
            .UseOrdinalStringComparison(dbContext);

        modelBuilder.Entity<OrganizationEnrollmentClaim>()
            .Property(claim => claim.SubjectId)
            .UseOrdinalStringComparison(dbContext);
        modelBuilder.Entity<OrganizationEnrollmentClaim>()
            .Property(claim => claim.LastChangedBy)
            .UseOrdinalStringComparison(dbContext);
    }
}
