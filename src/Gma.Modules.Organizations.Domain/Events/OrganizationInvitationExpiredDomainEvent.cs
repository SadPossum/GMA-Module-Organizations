namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;

public sealed record OrganizationInvitationExpiredDomainEvent : DomainEvent
{
    public OrganizationInvitationExpiredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid organizationId,
        Guid invitationId,
        DateTimeOffset expiresAtUtc,
        long invitationVersion)
        : base(eventId, occurredAtUtc)
    {
        this.OrganizationId = OrganizationDomainEventGuards.RequireId(
            organizationId,
            nameof(organizationId));
        this.InvitationId = OrganizationDomainEventGuards.RequireId(invitationId, nameof(invitationId));
        this.ExpiresAtUtc = OrganizationDomainEventGuards.RequireReachedDeadline(
            expiresAtUtc,
            occurredAtUtc,
            nameof(expiresAtUtc));
        this.InvitationVersion = OrganizationDomainEventGuards.RequirePositiveVersion(
            invitationVersion,
            nameof(invitationVersion));
    }

    public Guid OrganizationId { get; }
    public Guid InvitationId { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public long InvitationVersion { get; }
}
