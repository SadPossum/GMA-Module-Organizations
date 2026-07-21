namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationJoinSourceIssuer
{
    Task<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> IssueInvitationAsync(
        OrganizationInvitationIssuanceRequest request,
        CancellationToken cancellationToken = default);

    Task<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> IssueEnrollmentLinkAsync(
        OrganizationEnrollmentLinkIssuanceRequest request,
        CancellationToken cancellationToken = default);
}
