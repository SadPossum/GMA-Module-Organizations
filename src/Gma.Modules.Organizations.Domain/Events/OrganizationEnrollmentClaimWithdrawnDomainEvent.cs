namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;

public sealed record OrganizationEnrollmentClaimWithdrawnDomainEvent : DomainEvent
{
    public OrganizationEnrollmentClaimWithdrawnDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid organizationId,
        Guid enrollmentLinkId,
        Guid claimId,
        long claimVersion)
        : base(eventId, occurredAtUtc)
    {
        this.OrganizationId = OrganizationDomainEventGuards.RequireId(
            organizationId,
            nameof(organizationId));
        this.EnrollmentLinkId = OrganizationDomainEventGuards.RequireId(
            enrollmentLinkId,
            nameof(enrollmentLinkId));
        this.ClaimId = OrganizationDomainEventGuards.RequireId(claimId, nameof(claimId));
        this.ClaimVersion = OrganizationDomainEventGuards.RequirePositiveVersion(
            claimVersion,
            nameof(claimVersion));
    }

    public Guid OrganizationId { get; }
    public Guid EnrollmentLinkId { get; }
    public Guid ClaimId { get; }
    public long ClaimVersion { get; }
}
