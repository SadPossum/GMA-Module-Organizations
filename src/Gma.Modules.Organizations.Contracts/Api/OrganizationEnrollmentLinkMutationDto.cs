namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentLinkMutationDto(
    OrganizationEnrollmentLinkDto EnrollmentLink,
    string? ReplacementToken);
