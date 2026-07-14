namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentPreviewDto(
    Guid EnrollmentLinkId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    DateTimeOffset ExpiresAtUtc,
    int RemainingClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode,
    OrganizationEnrollmentLinkStatus Status);
