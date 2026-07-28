namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record OrganizationEnrollmentClaimExpiredIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "enrollment-claim-expired";
    public const int EventVersion = 1;

    public OrganizationEnrollmentClaimExpiredIntegrationEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid organizationId,
        Guid enrollmentLinkId,
        Guid claimId,
        DateTimeOffset decisionExpiresAtUtc,
        long claimVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.OrganizationId = IntegrationEventContractGuards.RequireId(organizationId, nameof(organizationId));
        this.EnrollmentLinkId = IntegrationEventContractGuards.RequireId(
            enrollmentLinkId, nameof(enrollmentLinkId));
        this.ClaimId = IntegrationEventContractGuards.RequireId(claimId, nameof(claimId));
        this.DecisionExpiresAtUtc = OrganizationInvitationExpiredIntegrationEvent.RequireEffectiveDeadline(
            decisionExpiresAtUtc, occurredAtUtc, nameof(decisionExpiresAtUtc));
        this.ClaimVersion = claimVersion > 0
            ? claimVersion
            : throw new ArgumentOutOfRangeException(nameof(claimVersion));
    }

    public string ScopeId { get; }
    public Guid OrganizationId { get; }
    public Guid EnrollmentLinkId { get; }
    public Guid ClaimId { get; }
    public DateTimeOffset DecisionExpiresAtUtc { get; }
    public long ClaimVersion { get; }
}
