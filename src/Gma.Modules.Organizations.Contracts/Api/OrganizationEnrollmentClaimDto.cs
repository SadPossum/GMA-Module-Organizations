namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentClaimDto(
    Guid ClaimId,
    Guid EnrollmentLinkId,
    Guid OrganizationId,
    string SubjectId,
    OrganizationEnrollmentClaimStatus Status,
    Guid? MembershipId,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);
