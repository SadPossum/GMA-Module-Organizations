namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationInvitationChangedDomainEvent : DomainEvent
{
    public OrganizationInvitationChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid organizationId,
        Guid invitationId,
        OrganizationInvitationChangeKind changeKind,
        OrganizationInvitationState status,
        string? acceptedSubjectId,
        long invitationVersion)
        : base(eventId, occurredAtUtc)
    {
        this.OrganizationId = OrganizationDomainEventGuards.RequireId(
            organizationId,
            nameof(organizationId));
        this.InvitationId = OrganizationDomainEventGuards.RequireId(invitationId, nameof(invitationId));
        this.ChangeKind = OrganizationDomainEventGuards.RequireKnown(changeKind, nameof(changeKind));
        this.Status = OrganizationDomainEventGuards.RequireKnown(status, nameof(status));
        this.AcceptedSubjectId = acceptedSubjectId is null
            ? null
            : OrganizationDomainEventGuards.RequireSubjectId(
                acceptedSubjectId,
                nameof(acceptedSubjectId));
        this.InvitationVersion = OrganizationDomainEventGuards.RequirePositiveVersion(
            invitationVersion,
            nameof(invitationVersion));
    }

    public Guid OrganizationId { get; }
    public Guid InvitationId { get; }
    public OrganizationInvitationChangeKind ChangeKind { get; }
    public OrganizationInvitationState Status { get; }
    public string? AcceptedSubjectId { get; }
    public long InvitationVersion { get; }
}
