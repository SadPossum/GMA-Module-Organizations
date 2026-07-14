namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentLinkDto(
    Guid EnrollmentLinkId,
    Guid OrganizationId,
    string CreatorSubjectId,
    DateTimeOffset ExpiresAtUtc,
    int MaximumClaims,
    int ReservedClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode,
    OrganizationEnrollmentLinkStatus Status,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);
