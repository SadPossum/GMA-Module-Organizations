namespace Gma.Modules.Organizations.Persistence.Repositories;

using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationRepository(OrganizationsDbContext dbContext) : IOrganizationRepository
{
    public Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.Organizations.SingleOrDefaultAsync(
            organization => organization.Id == organizationId, cancellationToken);

    public Task<OrganizationMembership?> GetMembershipAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
        return dbContext.Memberships.SingleOrDefaultAsync(
            membership => membership.OrganizationId == organizationId &&
                          membership.SubjectId == normalizedSubject,
            cancellationToken);
    }

    public Task<OrganizationInvitation?> GetInvitationAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        dbContext.Invitations.SingleOrDefaultAsync(
            invitation => invitation.OrganizationId == organizationId && invitation.Id == invitationId,
            cancellationToken);

    public Task<bool> InvitationIdExistsAsync(
        Guid invitationId,
        CancellationToken cancellationToken) => dbContext.Invitations
        .AsNoTracking()
        .AnyAsync(invitation => invitation.Id == invitationId, cancellationToken);

    public Task<OrganizationInvitation?> GetInvitationByDigestAsync(
        string tokenDigest,
        CancellationToken cancellationToken) =>
        dbContext.Invitations.SingleOrDefaultAsync(
            invitation => invitation.TokenDigest == tokenDigest,
            cancellationToken);

    public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkAsync(
        Guid organizationId,
        Guid enrollmentLinkId,
        CancellationToken cancellationToken) =>
        dbContext.EnrollmentLinks.SingleOrDefaultAsync(
            link => link.OrganizationId == organizationId && link.Id == enrollmentLinkId,
            cancellationToken);

    public Task<bool> EnrollmentLinkIdExistsAsync(
        Guid enrollmentLinkId,
        CancellationToken cancellationToken) => dbContext.EnrollmentLinks
        .AsNoTracking()
        .AnyAsync(link => link.Id == enrollmentLinkId, cancellationToken);

    public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkByDigestAsync(
        string tokenDigest,
        CancellationToken cancellationToken) =>
        dbContext.EnrollmentLinks.SingleOrDefaultAsync(
            link => link.TokenDigest == tokenDigest,
            cancellationToken);

    public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimAsync(
        Guid organizationId,
        Guid claimId,
        CancellationToken cancellationToken) =>
        dbContext.EnrollmentClaims.SingleOrDefaultAsync(
            claim => claim.OrganizationId == organizationId && claim.Id == claimId,
            cancellationToken);

    public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimBySubjectAsync(
        Guid enrollmentLinkId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
        return dbContext.EnrollmentClaims.SingleOrDefaultAsync(
            claim => claim.EnrollmentLinkId == enrollmentLinkId &&
                     claim.SubjectId == normalizedSubject,
            cancellationToken);
    }

    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludingOrganizationId,
        CancellationToken cancellationToken) =>
        dbContext.Organizations.AnyAsync(
            organization => organization.Slug == slug &&
                            (!excludingOrganizationId.HasValue || organization.Id != excludingOrganizationId.Value),
            cancellationToken);

    public Task<bool> MembershipExistsAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
        return dbContext.Memberships.AnyAsync(
            membership => membership.OrganizationId == organizationId &&
                          membership.SubjectId == normalizedSubject,
            cancellationToken);
    }

    public async Task<OrganizationListResponse> ListForSubjectAsync(
        string subjectId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
        var rows = await (
            from membership in dbContext.Memberships.AsNoTracking()
            join organization in dbContext.Organizations.AsNoTracking()
                on membership.OrganizationId equals organization.Id
            where membership.SubjectId == normalizedSubject &&
                  membership.Status == OrganizationMembershipState.Active &&
                  organization.Status != OrganizationState.Archived
            orderby organization.Name, organization.Id
            select new { Organization = organization, Membership = membership })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new OrganizationListResponse(
            rows.Select(row => new OrganizationMembershipSummaryDto(
                row.Organization.ToDto(), row.Membership.ToDto())).ToArray(),
            page,
            pageSize);
    }

    public async Task<OrganizationCatalogListResponse> ListCatalogAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Organization[] organizations = await dbContext.Organizations
            .AsNoTracking()
            .OrderBy(organization => organization.Name)
            .ThenBy(organization => organization.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new OrganizationCatalogListResponse(
            organizations.Select(organization => organization.ToDto()).ToArray(), page, pageSize);
    }

    public async Task<OrganizationMemberListResponse> ListMembersAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        OrganizationMembershipDto[] members = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId &&
                                 membership.Status != OrganizationMembershipState.Removed)
            .OrderByDescending(membership => membership.Role)
            .ThenBy(membership => membership.SubjectId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(membership => new OrganizationMembershipDto(
                membership.Id,
                membership.OrganizationId,
                membership.SubjectId,
                OrganizationMappings.MapRole(membership.Role),
                OrganizationMappings.MapStatus(membership.Status),
                membership.Version,
                membership.JoinedAtUtc,
                membership.LastChangedAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new OrganizationMemberListResponse(members, page, pageSize);
    }

    public async Task<OrganizationInvitationListResponse> ListInvitationsAsync(
        Guid organizationId,
        int page,
        int pageSize,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        OrganizationInvitation[] invitations = await dbContext.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.OrganizationId == organizationId)
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .ThenBy(invitation => invitation.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new OrganizationInvitationListResponse(
            invitations.Select(invitation => invitation.ToDto(nowUtc)).ToArray(),
            page,
            pageSize);
    }

    public async Task<OrganizationEnrollmentLinkListResponse> ListEnrollmentLinksAsync(
        Guid organizationId,
        int page,
        int pageSize,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        OrganizationEnrollmentLink[] links = await dbContext.EnrollmentLinks
            .AsNoTracking()
            .Where(link => link.OrganizationId == organizationId)
            .OrderByDescending(link => link.CreatedAtUtc)
            .ThenBy(link => link.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new OrganizationEnrollmentLinkListResponse(
            links.Select(link => link.ToDto(nowUtc)).ToArray(), page, pageSize);
    }

    public async Task<OrganizationJoinRequestListResponse> ListPendingJoinRequestsAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        OrganizationEnrollmentClaimDto[] claims = await dbContext.EnrollmentClaims
            .AsNoTracking()
            .Where(claim => claim.OrganizationId == organizationId &&
                            claim.Status == OrganizationEnrollmentClaimState.Pending)
            .OrderBy(claim => claim.CreatedAtUtc)
            .ThenBy(claim => claim.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(claim => new OrganizationEnrollmentClaimDto(
                claim.Id, claim.EnrollmentLinkId, claim.OrganizationId, claim.SubjectId,
                OrganizationEnrollmentClaimStatus.Pending, claim.MembershipId,
                claim.Version, claim.CreatedAtUtc, claim.LastChangedAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new OrganizationJoinRequestListResponse(claims, page, pageSize);
    }

    public async Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken) =>
        await dbContext.Organizations.AddAsync(organization, cancellationToken).ConfigureAwait(false);

    public async Task AddMembershipAsync(
        OrganizationMembership membership,
        CancellationToken cancellationToken) =>
        await dbContext.Memberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);

    public async Task AddInvitationAsync(
        OrganizationInvitation invitation,
        CancellationToken cancellationToken) =>
        await dbContext.Invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);

    public async Task AddEnrollmentLinkAsync(
        OrganizationEnrollmentLink enrollmentLink,
        CancellationToken cancellationToken) =>
        await dbContext.EnrollmentLinks.AddAsync(enrollmentLink, cancellationToken).ConfigureAwait(false);

    public async Task AddEnrollmentClaimAsync(
        OrganizationEnrollmentClaim claim,
        CancellationToken cancellationToken) =>
        await dbContext.EnrollmentClaims.AddAsync(claim, cancellationToken).ConfigureAwait(false);
}
