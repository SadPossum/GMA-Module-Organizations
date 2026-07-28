namespace Gma.Modules.Organizations.Persistence.Repositories;

using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationLifecycleRepository(OrganizationsDbContext dbContext)
    : IOrganizationLifecycleRepository
{
    public Task<OrganizationInvitation[]> ListDueInvitationsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken) =>
        dbContext.Invitations
            .Where(invitation =>
                invitation.Status == OrganizationInvitationState.Pending &&
                invitation.ExpiresAtUtc <= nowUtc)
            .OrderBy(invitation => invitation.ExpiresAtUtc)
            .ThenBy(invitation => invitation.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

    public async Task<OrganizationEnrollmentClaimExpiryCandidate[]> ListDueEnrollmentClaimsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var candidates = await (
            from claim in dbContext.EnrollmentClaims
            join link in dbContext.EnrollmentLinks
                on claim.EnrollmentLinkId equals link.Id
            where claim.Status == OrganizationEnrollmentClaimState.Pending &&
                  claim.DecisionExpiresAtUtc <= nowUtc
            orderby claim.DecisionExpiresAtUtc, claim.Id
            select new { Claim = claim, Link = link })
            .Take(batchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Select(candidate => new OrganizationEnrollmentClaimExpiryCandidate(
                candidate.Claim, candidate.Link))
            .ToArray();
    }

    public Task<OrganizationEnrollmentLink[]> ListDueEnrollmentLinksAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken) =>
        dbContext.EnrollmentLinks
            .Where(link =>
                link.Status == OrganizationEnrollmentLinkState.Active &&
                link.ExpiresAtUtc <= nowUtc)
            .OrderBy(link => link.ExpiresAtUtc)
            .ThenBy(link => link.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
}
