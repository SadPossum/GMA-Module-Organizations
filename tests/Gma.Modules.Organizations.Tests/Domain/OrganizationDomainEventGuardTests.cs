namespace Gma.Modules.Organizations.Tests;

using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationDomainEventGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Organization_change_rejects_invalid_identity_state_and_version()
    {
        Assert.Throws<ArgumentException>(() => new OrganizationChangedDomainEvent(
            Guid.NewGuid(), Now, Guid.Empty, OrganizationChangeKind.Created,
            OrganizationState.Active, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationChangedDomainEvent(
            Guid.NewGuid(), Now, Guid.NewGuid(), OrganizationChangeKind.Unknown,
            OrganizationState.Active, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationChangedDomainEvent(
            Guid.NewGuid(), Now, Guid.NewGuid(), OrganizationChangeKind.Created,
            OrganizationState.Active, 0));
    }

    [Fact]
    public void Membership_change_normalizes_valid_subject_and_rejects_invalid_subject()
    {
        OrganizationMembershipChangedDomainEvent valid = new(
            Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), "  subject-a  ",
            OrganizationMembershipChangeKind.Joined, OrganizationMembershipRole.Member,
            OrganizationMembershipState.Active, 1);

        Assert.Equal("subject-a", valid.SubjectId);
        Assert.Throws<ArgumentException>(() => new OrganizationMembershipChangedDomainEvent(
            Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), "invalid subject",
            OrganizationMembershipChangeKind.Joined, OrganizationMembershipRole.Member,
            OrganizationMembershipState.Active, 1));
    }

    [Fact]
    public void Enrollment_claim_change_rejects_empty_optional_membership_id()
    {
        Assert.Throws<ArgumentException>(() => new OrganizationEnrollmentClaimChangedDomainEvent(
            Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "subject-a",
            OrganizationEnrollmentClaimChangeKind.Accepted, OrganizationEnrollmentClaimState.Accepted,
            Guid.Empty, 1));
    }

    [Fact]
    public void Expiry_events_require_a_reached_deadline()
    {
        DateTimeOffset future = Now.AddMinutes(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationInvitationExpiredDomainEvent(
            Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), future, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationEnrollmentLinkExpiredDomainEvent(
            Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), future, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationEnrollmentClaimExpiredDomainEvent(
            Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), future, 1));
    }

    [Fact]
    public void Expiry_events_accept_the_occurrence_time_as_the_deadline()
    {
        OrganizationInvitationExpiredDomainEvent expired = new(
            Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), Now, 1);

        Assert.Equal(Now, expired.ExpiresAtUtc);
    }
}
