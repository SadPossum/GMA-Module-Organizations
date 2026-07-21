namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationJoinSourceManager
{
    Task<OrganizationJoinSourceOperation<OrganizationInvitationListResponse>> ListInvitationsAsync(
        OrganizationJoinSourceListRequest request,
        CancellationToken cancellationToken = default);

    Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkListResponse>> ListEnrollmentLinksAsync(
        OrganizationJoinSourceListRequest request,
        CancellationToken cancellationToken = default);

    Task<OrganizationJoinSourceOperation<OrganizationInvitationDto>> RevokeInvitationAsync(
        OrganizationInvitationRevocationRequest request,
        CancellationToken cancellationToken = default);

    Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>> DisableEnrollmentLinkAsync(
        OrganizationEnrollmentLinkDisableRequest request,
        CancellationToken cancellationToken = default);
}
