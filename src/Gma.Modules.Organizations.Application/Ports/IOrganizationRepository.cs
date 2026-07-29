namespace Gma.Modules.Organizations.Application.Ports;

using Gma.Framework.Pagination;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

public interface IOrganizationRepository
{
    Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, string subjectId, CancellationToken cancellationToken);
    Task<OrganizationInvitation?> GetInvitationAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken);
    Task<bool> InvitationIdExistsAsync(Guid invitationId, CancellationToken cancellationToken);
    Task<OrganizationInvitation?> GetInvitationByDigestAsync(string tokenDigest, CancellationToken cancellationToken);
    Task<OrganizationEnrollmentLink?> GetEnrollmentLinkAsync(Guid organizationId, Guid enrollmentLinkId, CancellationToken cancellationToken);
    Task<bool> EnrollmentLinkIdExistsAsync(Guid enrollmentLinkId, CancellationToken cancellationToken);
    Task<OrganizationEnrollmentLink?> GetEnrollmentLinkByDigestAsync(string tokenDigest, CancellationToken cancellationToken);
    Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimAsync(Guid organizationId, Guid claimId, CancellationToken cancellationToken);
    Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimBySubjectAsync(Guid enrollmentLinkId, string subjectId, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, Guid? excludingOrganizationId, CancellationToken cancellationToken);
    Task<bool> MembershipExistsAsync(Guid organizationId, string subjectId, CancellationToken cancellationToken);
    Task<OrganizationListResponse> ListForSubjectAsync(
        string subjectId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<OrganizationCatalogListResponse> ListCatalogAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<OrganizationMemberListResponse> ListMembersAsync(
        Guid organizationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<OrganizationInvitationListResponse> ListInvitationsAsync(
        Guid organizationId,
        PageRequest pageRequest,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task<OrganizationEnrollmentLinkListResponse> ListEnrollmentLinksAsync(
        Guid organizationId,
        PageRequest pageRequest,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task<OrganizationJoinRequestListResponse> ListPendingJoinRequestsAsync(
        Guid organizationId,
        PageRequest pageRequest,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken);
    Task AddMembershipAsync(OrganizationMembership membership, CancellationToken cancellationToken);
    Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken);
    Task AddEnrollmentLinkAsync(OrganizationEnrollmentLink enrollmentLink, CancellationToken cancellationToken);
    Task AddEnrollmentClaimAsync(OrganizationEnrollmentClaim claim, CancellationToken cancellationToken);
}
