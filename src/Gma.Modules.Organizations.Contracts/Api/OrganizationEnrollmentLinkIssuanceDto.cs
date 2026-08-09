namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentLinkIssuanceDto(
    OrganizationEnrollmentLinkDto EnrollmentLink,
    string? Token,
    OrganizationJoinSourceIssuanceOutcome Outcome);
