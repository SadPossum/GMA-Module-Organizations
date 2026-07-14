namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record OrganizationInvitationChangedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "invitation-changed";
    public const int EventVersion = 1;

    public OrganizationInvitationChangedIntegrationEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid organizationId,
        Guid invitationId,
        OrganizationInvitationChange change,
        OrganizationInvitationStatus status,
        string? acceptedSubjectId,
        long invitationVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.OrganizationId = IntegrationEventContractGuards.RequireId(organizationId, nameof(organizationId));
        this.InvitationId = IntegrationEventContractGuards.RequireId(invitationId, nameof(invitationId));
        this.Change = change is > OrganizationInvitationChange.Unknown and <= OrganizationInvitationChange.Superseded
            ? change
            : throw new ArgumentOutOfRangeException(nameof(change));
        this.Status = status is > OrganizationInvitationStatus.Unknown and < OrganizationInvitationStatus.Expired
            ? status
            : throw new ArgumentOutOfRangeException(nameof(status));
        this.AcceptedSubjectId = NormalizeOptionalSubject(acceptedSubjectId);
        this.InvitationVersion = invitationVersion > 0
            ? invitationVersion
            : throw new ArgumentOutOfRangeException(nameof(invitationVersion));
    }

    public string ScopeId { get; }
    public Guid OrganizationId { get; }
    public Guid InvitationId { get; }
    public OrganizationInvitationChange Change { get; }
    public OrganizationInvitationStatus Status { get; }
    public string? AcceptedSubjectId { get; }
    public long InvitationVersion { get; }

    private static string? NormalizeOptionalSubject(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == 0 ? null : normalized.Length <= 160 &&
            normalized.All(character => !char.IsWhiteSpace(character) && !char.IsControl(character))
                ? normalized
                : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
