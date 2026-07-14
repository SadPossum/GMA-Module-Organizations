namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record OrganizationMembershipChangedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "membership-changed";
    public const int EventVersion = 1;

    public OrganizationMembershipChangedIntegrationEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid organizationId,
        Guid membershipId,
        string subjectId,
        OrganizationMembershipChange change,
        OrganizationMembershipRole role,
        OrganizationMembershipStatus status,
        long membershipVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.OrganizationId = IntegrationEventContractGuards.RequireId(organizationId, nameof(organizationId));
        this.MembershipId = IntegrationEventContractGuards.RequireId(membershipId, nameof(membershipId));
        this.SubjectId = NormalizeSubject(subjectId);
        this.Change = change is > OrganizationMembershipChange.Unknown and <= OrganizationMembershipChange.Restored
            ? change
            : throw new ArgumentOutOfRangeException(nameof(change));
        this.Role = role is OrganizationMembershipRole.Member or OrganizationMembershipRole.Owner
            ? role
            : throw new ArgumentOutOfRangeException(nameof(role));
        this.Status = status is > OrganizationMembershipStatus.Unknown and <= OrganizationMembershipStatus.Removed
            ? status
            : throw new ArgumentOutOfRangeException(nameof(status));
        this.MembershipVersion = membershipVersion > 0
            ? membershipVersion
            : throw new ArgumentOutOfRangeException(nameof(membershipVersion));
    }

    public string ScopeId { get; }
    public Guid OrganizationId { get; }
    public Guid MembershipId { get; }
    public string SubjectId { get; }
    public OrganizationMembershipChange Change { get; }
    public OrganizationMembershipRole Role { get; }
    public OrganizationMembershipStatus Status { get; }
    public long MembershipVersion { get; }

    private static string NormalizeSubject(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is >= 1 and <= 160 &&
               normalized.All(character => !char.IsWhiteSpace(character) && !char.IsControl(character))
            ? normalized
            : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
