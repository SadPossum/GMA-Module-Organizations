namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record OrganizationChangedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "organization-changed";
    public const int EventVersion = 1;

    public OrganizationChangedIntegrationEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid organizationId,
        OrganizationChange change,
        OrganizationStatus status,
        long organizationVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.OrganizationId = IntegrationEventContractGuards.RequireId(organizationId, nameof(organizationId));
        this.Change = change is > OrganizationChange.Unknown and <= OrganizationChange.OwnershipTransferred
            ? change
            : throw new ArgumentOutOfRangeException(nameof(change));
        this.Status = status is > OrganizationStatus.Unknown and <= OrganizationStatus.Archived
            ? status
            : throw new ArgumentOutOfRangeException(nameof(status));
        this.OrganizationVersion = organizationVersion > 0
            ? organizationVersion
            : throw new ArgumentOutOfRangeException(nameof(organizationVersion));
    }

    public string ScopeId { get; }
    public Guid OrganizationId { get; }
    public OrganizationChange Change { get; }
    public OrganizationStatus Status { get; }
    public long OrganizationVersion { get; }
}
