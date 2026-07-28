namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record OrganizationInvitationExpiredIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "invitation-expired";
    public const int EventVersion = 1;

    public OrganizationInvitationExpiredIntegrationEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid organizationId,
        Guid invitationId,
        DateTimeOffset expiresAtUtc,
        long invitationVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.OrganizationId = IntegrationEventContractGuards.RequireId(organizationId, nameof(organizationId));
        this.InvitationId = IntegrationEventContractGuards.RequireId(invitationId, nameof(invitationId));
        this.ExpiresAtUtc = RequireEffectiveDeadline(expiresAtUtc, occurredAtUtc, nameof(expiresAtUtc));
        this.InvitationVersion = invitationVersion > 0
            ? invitationVersion
            : throw new ArgumentOutOfRangeException(nameof(invitationVersion));
    }

    public string ScopeId { get; }
    public Guid OrganizationId { get; }
    public Guid InvitationId { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public long InvitationVersion { get; }

    internal static DateTimeOffset RequireEffectiveDeadline(
        DateTimeOffset value,
        DateTimeOffset occurredAtUtc,
        string parameterName) =>
        value != default && value <= occurredAtUtc
            ? value
            : throw new ArgumentOutOfRangeException(parameterName);
}
