namespace Gma.Modules.Organizations.Tests.Contracts;

using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationExpiryIntegrationEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Expiry_events_are_versioned_and_payload_minimal()
    {
        Guid organizationId = Guid.NewGuid();
        OrganizationInvitationExpiredIntegrationEvent invitation = new(
            Guid.NewGuid(), Now, organizationId.ToString("D"), organizationId,
            Guid.NewGuid(), Now.AddMinutes(-1), 2);
        OrganizationEnrollmentLinkExpiredIntegrationEvent link = new(
            Guid.NewGuid(), Now, organizationId.ToString("D"), organizationId,
            Guid.NewGuid(), Now.AddMinutes(-1), 3);
        OrganizationEnrollmentClaimExpiredIntegrationEvent claim = new(
            Guid.NewGuid(), Now, organizationId.ToString("D"), organizationId,
            link.EnrollmentLinkId, Guid.NewGuid(), Now.AddMinutes(-1), 2);

        Assert.Equal(1, invitation.Version);
        Assert.Equal(1, link.Version);
        Assert.Equal(1, claim.Version);
        Assert.DoesNotContain(
            typeof(OrganizationEnrollmentClaimExpiredIntegrationEvent).GetProperties(),
            property => property.Name.Contains("Subject", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(OrganizationInvitationExpiredIntegrationEvent).GetProperties(),
            property => property.Name.Contains("Recipient", StringComparison.Ordinal));
    }

    [Fact]
    public void Expiry_events_reject_a_deadline_after_the_occurrence_time()
    {
        Guid organizationId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OrganizationEnrollmentLinkExpiredIntegrationEvent(
                Guid.NewGuid(), Now, organizationId.ToString("D"), organizationId,
                Guid.NewGuid(), Now.AddMinutes(1), 2));
    }
}
