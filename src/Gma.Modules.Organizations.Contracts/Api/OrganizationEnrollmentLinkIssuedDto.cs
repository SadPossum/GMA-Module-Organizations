namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentLinkIssuedDto(
    OrganizationEnrollmentLinkDto EnrollmentLink,
    string Token);
