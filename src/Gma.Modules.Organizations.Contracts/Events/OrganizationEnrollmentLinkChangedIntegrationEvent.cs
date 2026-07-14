namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record OrganizationEnrollmentLinkChangedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "enrollment-link-changed";
    public const int EventVersion = 1;

    public OrganizationEnrollmentLinkChangedIntegrationEvent(
        Guid eventId, DateTimeOffset occurredAtUtc, string scopeId,
        Guid organizationId, Guid enrollmentLinkId,
        OrganizationEnrollmentLinkChange change, OrganizationEnrollmentLinkStatus status,
        int reservedClaims, long linkVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.OrganizationId = IntegrationEventContractGuards.RequireId(organizationId, nameof(organizationId));
        this.EnrollmentLinkId = IntegrationEventContractGuards.RequireId(enrollmentLinkId, nameof(enrollmentLinkId));
        this.Change = change is > OrganizationEnrollmentLinkChange.Unknown and <= OrganizationEnrollmentLinkChange.Rotated
            ? change : throw new ArgumentOutOfRangeException(nameof(change));
        this.Status = status is > OrganizationEnrollmentLinkStatus.Unknown and <= OrganizationEnrollmentLinkStatus.Rotated
            ? status : throw new ArgumentOutOfRangeException(nameof(status));
        this.ReservedClaims = reservedClaims >= 0 ? reservedClaims : throw new ArgumentOutOfRangeException(nameof(reservedClaims));
        this.LinkVersion = linkVersion > 0 ? linkVersion : throw new ArgumentOutOfRangeException(nameof(linkVersion));
    }

    public string ScopeId { get; }
    public Guid OrganizationId { get; }
    public Guid EnrollmentLinkId { get; }
    public OrganizationEnrollmentLinkChange Change { get; }
    public OrganizationEnrollmentLinkStatus Status { get; }
    public int ReservedClaims { get; }
    public long LinkVersion { get; }
}
