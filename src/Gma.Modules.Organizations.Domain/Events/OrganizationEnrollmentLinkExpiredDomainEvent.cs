namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;

public sealed record OrganizationEnrollmentLinkExpiredDomainEvent : DomainEvent
{
    public OrganizationEnrollmentLinkExpiredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid organizationId,
        Guid enrollmentLinkId,
        DateTimeOffset expiresAtUtc,
        long linkVersion)
        : base(eventId, occurredAtUtc)
    {
        this.OrganizationId = OrganizationDomainEventGuards.RequireId(
            organizationId,
            nameof(organizationId));
        this.EnrollmentLinkId = OrganizationDomainEventGuards.RequireId(
            enrollmentLinkId,
            nameof(enrollmentLinkId));
        this.ExpiresAtUtc = OrganizationDomainEventGuards.RequireReachedDeadline(
            expiresAtUtc,
            occurredAtUtc,
            nameof(expiresAtUtc));
        this.LinkVersion = OrganizationDomainEventGuards.RequirePositiveVersion(
            linkVersion,
            nameof(linkVersion));
    }

    public Guid OrganizationId { get; }
    public Guid EnrollmentLinkId { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public long LinkVersion { get; }
}
