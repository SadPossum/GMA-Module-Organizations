namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeEnrollmentLinkExportRecord(
    Guid EnrollmentLinkId,
    Guid OrganizationId,
    string CreatorSubjectId,
    int TokenVersion,
    DateTimeOffset ExpiresAtUtc,
    int MaximumClaims,
    int ReservedClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode,
    OrganizationEnrollmentLinkStatus Status,
    long Version,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc,
    Guid? ReplacesEnrollmentLinkId = null,
    long? ReplacesEnrollmentLinkVersion = null)
    : OrganizationScopeExportRecord;
