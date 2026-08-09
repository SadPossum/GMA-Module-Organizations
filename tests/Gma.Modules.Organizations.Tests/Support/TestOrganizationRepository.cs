namespace Gma.Modules.Organizations.Tests.Support;

using Gma.Framework.Pagination;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;

internal sealed class TestOrganizationRepository(
    Organization organization,
    OrganizationMembership owner) : IOrganizationRepository
{
    public List<Organization> Organizations { get; } = [organization];
    public List<OrganizationMembership> Memberships { get; } = [owner];
    public List<OrganizationInvitation> Invitations { get; } = [];
    public List<OrganizationEnrollmentLink> EnrollmentLinks { get; } = [];
    public List<OrganizationEnrollmentClaim> EnrollmentClaims { get; } = [];
    public int MembershipReadCount { get; private set; }
    public int InvitationReadCount { get; private set; }
    public int EnrollmentLinkReadCount { get; private set; }
    public Action? OnGovernanceRead { get; set; }

    public Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        this.OnGovernanceRead?.Invoke();
        return Task.FromResult(this.Organizations.SingleOrDefault(item => item.Id == organizationId));
    }

    public Task<OrganizationMembership?> GetMembershipAsync(
        Guid organizationId, string subjectId, CancellationToken cancellationToken)
    {
        this.OnGovernanceRead?.Invoke();
        this.MembershipReadCount++;
        return Task.FromResult(this.Memberships.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.SubjectId == subjectId.Trim()));
    }

    public Task<OrganizationInvitation?> GetInvitationAsync(
        Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
    {
        this.InvitationReadCount++;
        return Task.FromResult(this.Invitations.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.Id == invitationId));
    }

    public Task<bool> InvitationIdExistsAsync(
        Guid invitationId,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.Invitations.Any(item => item.Id == invitationId));

    public Task<OrganizationInvitation?> GetInvitationByDigestAsync(
        string tokenDigest, CancellationToken cancellationToken) =>
        Task.FromResult(this.Invitations.SingleOrDefault(item => item.TokenDigest == tokenDigest));

    public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkAsync(
        Guid organizationId, Guid enrollmentLinkId, CancellationToken cancellationToken)
    {
        this.EnrollmentLinkReadCount++;
        return Task.FromResult(this.EnrollmentLinks.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.Id == enrollmentLinkId));
    }

    public Task<bool> EnrollmentLinkIdExistsAsync(
        Guid enrollmentLinkId,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.EnrollmentLinks.Any(item => item.Id == enrollmentLinkId));

    public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkByDigestAsync(
        string tokenDigest, CancellationToken cancellationToken) =>
        Task.FromResult(this.EnrollmentLinks.SingleOrDefault(item => item.TokenDigest == tokenDigest));

    public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimAsync(
        Guid organizationId, Guid claimId, CancellationToken cancellationToken) =>
        Task.FromResult(this.EnrollmentClaims.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.Id == claimId));

    public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimBySubjectAsync(
        Guid enrollmentLinkId, string subjectId, CancellationToken cancellationToken) =>
        Task.FromResult(this.EnrollmentClaims.SingleOrDefault(item =>
            item.EnrollmentLinkId == enrollmentLinkId && item.SubjectId == subjectId.Trim()));

    public Task<bool> SlugExistsAsync(
        string slug, Guid? excludingOrganizationId, CancellationToken cancellationToken) =>
        Task.FromResult(this.Organizations.Any(item =>
            item.Slug == slug && item.Id != excludingOrganizationId));

    public Task<bool> MembershipExistsAsync(
        Guid organizationId, string subjectId, CancellationToken cancellationToken) =>
        Task.FromResult(this.Memberships.Any(item =>
            item.OrganizationId == organizationId && item.SubjectId == subjectId.Trim()));

    public Task<OrganizationListResponse> ListForSubjectAsync(
        string subjectId, PageRequest pageRequest, CancellationToken cancellationToken) =>
        Task.FromResult(new OrganizationListResponse([], pageRequest.Page, pageRequest.PageSize));

    public Task<OrganizationCatalogListResponse> ListCatalogAsync(
        PageRequest pageRequest, CancellationToken cancellationToken) =>
        Task.FromResult(new OrganizationCatalogListResponse(
            this.Organizations.Select(item => item.ToDto()).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize));

    public Task<OrganizationMemberListResponse> ListMembersAsync(
        Guid organizationId, PageRequest pageRequest, CancellationToken cancellationToken) =>
        Task.FromResult(new OrganizationMemberListResponse(
            [],
            pageRequest.Page,
            pageRequest.PageSize));

    public Task<OrganizationInvitationListResponse> ListInvitationsAsync(
        Guid organizationId, PageRequest pageRequest, DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult(new OrganizationInvitationListResponse(
            [],
            pageRequest.Page,
            pageRequest.PageSize));

    public Task<OrganizationEnrollmentLinkListResponse> ListEnrollmentLinksAsync(
        Guid organizationId, PageRequest pageRequest, DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult(new OrganizationEnrollmentLinkListResponse(
            this.EnrollmentLinks.Where(item => item.OrganizationId == organizationId)
                .Select(item => item.ToDto(nowUtc)).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize));

    public Task<OrganizationJoinRequestListResponse> ListPendingJoinRequestsAsync(
        Guid organizationId, PageRequest pageRequest, DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult(new OrganizationJoinRequestListResponse(
            this.EnrollmentClaims.Where(item => item.OrganizationId == organizationId &&
                                                 item.Status == OrganizationEnrollmentClaimState.Pending &&
                                                 item.DecisionExpiresAtUtc > nowUtc)
                .Select(OrganizationMappings.ToDto).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize));

    public Task AddOrganizationAsync(Organization value, CancellationToken cancellationToken) =>
        Add(this.Organizations, value);

    public Task AddMembershipAsync(OrganizationMembership value, CancellationToken cancellationToken) =>
        Add(this.Memberships, value);

    public Task AddInvitationAsync(OrganizationInvitation value, CancellationToken cancellationToken) =>
        Add(this.Invitations, value);

    public Task AddEnrollmentLinkAsync(OrganizationEnrollmentLink value, CancellationToken cancellationToken) =>
        Add(this.EnrollmentLinks, value);

    public Task AddEnrollmentClaimAsync(OrganizationEnrollmentClaim value, CancellationToken cancellationToken) =>
        Add(this.EnrollmentClaims, value);

    private static Task Add<T>(List<T> values, T value)
    {
        values.Add(value);
        return Task.CompletedTask;
    }
}
