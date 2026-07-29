namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationChangedDomainEvent : DomainEvent
{
    public OrganizationChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid organizationId,
        OrganizationChangeKind changeKind,
        OrganizationState status,
        long organizationVersion)
        : base(eventId, occurredAtUtc)
    {
        this.OrganizationId = OrganizationDomainEventGuards.RequireId(
            organizationId,
            nameof(organizationId));
        this.ChangeKind = OrganizationDomainEventGuards.RequireKnown(changeKind, nameof(changeKind));
        this.Status = OrganizationDomainEventGuards.RequireKnown(status, nameof(status));
        this.OrganizationVersion = OrganizationDomainEventGuards.RequirePositiveVersion(
            organizationVersion,
            nameof(organizationVersion));
    }

    public Guid OrganizationId { get; }
    public OrganizationChangeKind ChangeKind { get; }
    public OrganizationState Status { get; }
    public long OrganizationVersion { get; }
}
