namespace Gma.Modules.Organizations.Tests.Contracts;

using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationEnrollmentClaimWithdrawalIntegrationEventTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Withdrawal_event_is_versioned_and_payload_minimal()
    {
        Guid organizationId = Guid.NewGuid();
        OrganizationEnrollmentClaimWithdrawnIntegrationEvent integrationEvent = new(
            Guid.NewGuid(),
            Now,
            organizationId.ToString("D"),
            organizationId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            2);

        Assert.Equal(1, integrationEvent.Version);
        Assert.Equal(
            OrganizationEnrollmentClaimWithdrawnIntegrationEvent.EventType,
            integrationEvent.EventName);
        Assert.DoesNotContain(
            typeof(OrganizationEnrollmentClaimWithdrawnIntegrationEvent).GetProperties(),
            property => property.Name.Contains("Subject", StringComparison.Ordinal) ||
                        property.Name.Contains("Actor", StringComparison.Ordinal) ||
                        property.Name.Contains("Token", StringComparison.Ordinal));
    }

    [Fact]
    public void Withdrawal_event_rejects_invalid_identity_and_version()
    {
        Guid organizationId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            new OrganizationEnrollmentClaimWithdrawnIntegrationEvent(
                Guid.NewGuid(),
                Now,
                organizationId.ToString("D"),
                organizationId,
                Guid.Empty,
                Guid.NewGuid(),
                2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OrganizationEnrollmentClaimWithdrawnIntegrationEvent(
                Guid.NewGuid(),
                Now,
                organizationId.ToString("D"),
                organizationId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                0));
    }
}
