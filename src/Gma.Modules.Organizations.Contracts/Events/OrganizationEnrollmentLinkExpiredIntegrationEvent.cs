namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record OrganizationEnrollmentLinkExpiredIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "enrollment-link-expired";
    public const int EventVersion = 1;

    public OrganizationEnrollmentLinkExpiredIntegrationEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid organizationId,
        Guid enrollmentLinkId,
        DateTimeOffset expiresAtUtc,
        long linkVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.OrganizationId = IntegrationEventContractGuards.RequireId(organizationId, nameof(organizationId));
        this.EnrollmentLinkId = IntegrationEventContractGuards.RequireId(
            enrollmentLinkId, nameof(enrollmentLinkId));
        this.ExpiresAtUtc = OrganizationInvitationExpiredIntegrationEvent.RequireEffectiveDeadline(
            expiresAtUtc, occurredAtUtc, nameof(expiresAtUtc));
        this.LinkVersion = linkVersion > 0
            ? linkVersion
            : throw new ArgumentOutOfRangeException(nameof(linkVersion));
    }

    public string ScopeId { get; }
    public Guid OrganizationId { get; }
    public Guid EnrollmentLinkId { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public long LinkVersion { get; }
}
