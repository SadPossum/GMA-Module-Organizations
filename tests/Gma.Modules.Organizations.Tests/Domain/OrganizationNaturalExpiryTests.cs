namespace Gma.Modules.Organizations.Tests.Domain;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Domain.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationNaturalExpiryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Due_invitation_expires_once_with_a_payload_free_domain_fact()
    {
        OrganizationInvitation invitation = OrganizationInvitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "owner", "guest@example.test",
            new string('a', 64), Now.AddHours(1), "user:owner", Guid.NewGuid(), Now).Value;

        Assert.Equal(
            OrganizationDomainErrors.InvitationExpiryInvalid,
            invitation.Expire(invitation.Version, "system:lifecycle", Guid.NewGuid(), Now).Error);

        Assert.True(invitation.Expire(
            invitation.Version, "system:lifecycle", Guid.NewGuid(), Now.AddHours(1)).IsSuccess);

        Assert.Equal(OrganizationInvitationState.Expired, invitation.Status);
        OrganizationInvitationExpiredDomainEvent expired =
            Assert.Single(invitation.DomainEvents.OfType<OrganizationInvitationExpiredDomainEvent>());
        Assert.Equal(invitation.ExpiresAtUtc, expired.ExpiresAtUtc);
        Assert.DoesNotContain(
            typeof(OrganizationInvitationExpiredDomainEvent).GetProperties(),
            property => property.Name.Contains("Recipient", StringComparison.Ordinal));
    }

    [Fact]
    public void Due_link_expires_and_cannot_be_rotated_during_worker_lag()
    {
        OrganizationEnrollmentLink link = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), Guid.NewGuid(), "owner", new string('b', 64),
            Now.AddHours(1), 10, OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:owner", Guid.NewGuid(), Now).Value;

        Assert.Equal(
            OrganizationDomainErrors.EnrollmentLinkExpired,
            link.Rotate(link.Version, "user:owner", Guid.NewGuid(), Now.AddHours(1)).Error);
        Assert.True(link.Expire(
            link.Version, "system:lifecycle", Guid.NewGuid(), Now.AddHours(1)).IsSuccess);

        Assert.Equal(OrganizationEnrollmentLinkState.Expired, link.Status);
        Assert.Single(link.DomainEvents.OfType<OrganizationEnrollmentLinkExpiredDomainEvent>());
    }

    [Fact]
    public void Pending_claim_requires_a_future_decision_deadline_and_expires_without_subject_payload()
    {
        Guid organizationId = Guid.NewGuid();
        Guid linkId = Guid.NewGuid();
        var invalid = OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(), organizationId, linkId, "member",
            OrganizationEnrollmentClaimState.Pending, null,
            "user:member", Guid.NewGuid(), Now);
        OrganizationEnrollmentClaim claim = OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(), organizationId, linkId, "member",
            OrganizationEnrollmentClaimState.Pending, null,
            "user:member", Guid.NewGuid(), Now, Now.AddDays(7)).Value;

        Assert.Equal(OrganizationDomainErrors.EnrollmentClaimExpiryInvalid, invalid.Error);
        Assert.Equal(
            OrganizationDomainErrors.EnrollmentClaimExpired,
            claim.Approve(Guid.NewGuid(), claim.Version, "user:owner", Guid.NewGuid(), Now.AddDays(7)).Error);
        Assert.True(claim.Expire(
            claim.Version, "system:lifecycle", Guid.NewGuid(), Now.AddDays(7)).IsSuccess);

        Assert.Equal(OrganizationEnrollmentClaimState.Expired, claim.Status);
        OrganizationEnrollmentClaimExpiredDomainEvent expired =
            Assert.Single(claim.DomainEvents.OfType<OrganizationEnrollmentClaimExpiredDomainEvent>());
        Assert.Equal(claim.DecisionExpiresAtUtc, expired.DecisionExpiresAtUtc);
        Assert.DoesNotContain(
            typeof(OrganizationEnrollmentClaimExpiredDomainEvent).GetProperties(),
            property => property.Name.Contains("Subject", StringComparison.Ordinal));
    }
}
