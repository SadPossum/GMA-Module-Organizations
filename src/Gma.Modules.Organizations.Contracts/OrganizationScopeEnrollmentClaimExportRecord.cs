namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeEnrollmentClaimExportRecord(
    Guid EnrollmentClaimId,
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    string SubjectId,
    OrganizationEnrollmentClaimStatus Status,
    Guid? MembershipId,
    DateTimeOffset? DecisionExpiresAtUtc,
    long Version,
    DateTimeOffset CreatedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc)
    : OrganizationScopeExportRecord;
