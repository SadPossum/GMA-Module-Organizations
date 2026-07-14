namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record OrganizationEnrollmentClaimChangedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "enrollment-claim-changed";
    public const int EventVersion = 1;

    public OrganizationEnrollmentClaimChangedIntegrationEvent(
        Guid eventId, DateTimeOffset occurredAtUtc, string scopeId,
        Guid organizationId, Guid enrollmentLinkId, Guid claimId, string subjectId,
        OrganizationEnrollmentClaimChange change, OrganizationEnrollmentClaimStatus status,
        Guid? membershipId, long claimVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.OrganizationId = IntegrationEventContractGuards.RequireId(organizationId, nameof(organizationId));
        this.EnrollmentLinkId = IntegrationEventContractGuards.RequireId(enrollmentLinkId, nameof(enrollmentLinkId));
        this.ClaimId = IntegrationEventContractGuards.RequireId(claimId, nameof(claimId));
        this.SubjectId = NormalizeSubject(subjectId);
        this.Change = change is > OrganizationEnrollmentClaimChange.Unknown and <= OrganizationEnrollmentClaimChange.Rejected
            ? change : throw new ArgumentOutOfRangeException(nameof(change));
        this.Status = status is > OrganizationEnrollmentClaimStatus.Unknown and <= OrganizationEnrollmentClaimStatus.Rejected
            ? status : throw new ArgumentOutOfRangeException(nameof(status));
        this.MembershipId = membershipId;
        this.ClaimVersion = claimVersion > 0 ? claimVersion : throw new ArgumentOutOfRangeException(nameof(claimVersion));
    }

    public string ScopeId { get; }
    public Guid OrganizationId { get; }
    public Guid EnrollmentLinkId { get; }
    public Guid ClaimId { get; }
    public string SubjectId { get; }
    public OrganizationEnrollmentClaimChange Change { get; }
    public OrganizationEnrollmentClaimStatus Status { get; }
    public Guid? MembershipId { get; }
    public long ClaimVersion { get; }

    private static string NormalizeSubject(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is >= 1 and <= 160 &&
            normalized.All(character => !char.IsWhiteSpace(character) && !char.IsControl(character))
                ? normalized : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
