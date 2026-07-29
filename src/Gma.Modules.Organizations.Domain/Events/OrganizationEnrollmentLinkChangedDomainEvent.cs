namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationEnrollmentLinkChangedDomainEvent : DomainEvent
{
    public OrganizationEnrollmentLinkChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid organizationId,
        Guid enrollmentLinkId,
        OrganizationEnrollmentLinkChangeKind changeKind,
        OrganizationEnrollmentLinkState status,
        int reservedClaims,
        long linkVersion)
        : base(eventId, occurredAtUtc)
    {
        this.OrganizationId = OrganizationDomainEventGuards.RequireId(
            organizationId,
            nameof(organizationId));
        this.EnrollmentLinkId = OrganizationDomainEventGuards.RequireId(
            enrollmentLinkId,
            nameof(enrollmentLinkId));
        this.ChangeKind = OrganizationDomainEventGuards.RequireKnown(changeKind, nameof(changeKind));
        this.Status = OrganizationDomainEventGuards.RequireKnown(status, nameof(status));
        this.ReservedClaims = OrganizationDomainEventGuards.RequireNonNegative(
            reservedClaims,
            nameof(reservedClaims));
        this.LinkVersion = OrganizationDomainEventGuards.RequirePositiveVersion(
            linkVersion,
            nameof(linkVersion));
    }

    public Guid OrganizationId { get; }
    public Guid EnrollmentLinkId { get; }
    public OrganizationEnrollmentLinkChangeKind ChangeKind { get; }
    public OrganizationEnrollmentLinkState Status { get; }
    public int ReservedClaims { get; }
    public long LinkVersion { get; }
}
