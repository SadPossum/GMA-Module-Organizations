namespace Gma.Modules.Organizations.Persistence.Access;

using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationAccessDecisionReader(OrganizationsDbContext dbContext)
    : IOrganizationAccessDecisionReader, IOrganizationAccessCandidateFilter
{
    public async Task<OrganizationAccessDecision> ReadAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        string normalizedSubject = NormalizeSubjectId(subjectId);
        var access = await (
            from organization in dbContext.Organizations.AsNoTracking()
            join membership in dbContext.Memberships.AsNoTracking()
                    .Where(candidate => candidate.SubjectId == normalizedSubject)
                on organization.Id equals membership.OrganizationId into memberships
            from membership in memberships.DefaultIfEmpty()
            where organization.Id == organizationId
            select new
            {
                OrganizationStatus = organization.Status,
                MembershipStatus = membership == null
                    ? (OrganizationMembershipState?)null
                    : membership.Status
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (access is null)
        {
            return OrganizationAccessDecision.OrganizationNotFound;
        }

        if (access.OrganizationStatus != OrganizationState.Active)
        {
            return OrganizationAccessDecision.OrganizationInactive;
        }

        return access.MembershipStatus switch
        {
            null => OrganizationAccessDecision.MembershipNotFound,
            OrganizationMembershipState.Active => OrganizationAccessDecision.Allowed,
            _ => OrganizationAccessDecision.MembershipInactive
        };
    }

    public async Task<IReadOnlyList<string>> FilterAllowedAsync(
        Guid organizationId,
        IReadOnlyCollection<string> candidateSubjectIds,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(candidateSubjectIds);
        if (candidateSubjectIds.Count > OrganizationAccessContract.MaximumCandidateCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidateSubjectIds),
                candidateSubjectIds.Count,
                $"At most {OrganizationAccessContract.MaximumCandidateCount} candidates are allowed.");
        }

        string[] candidates = candidateSubjectIds
            .Select(NormalizeSubjectId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }

        return await (
                from organization in dbContext.Organizations.AsNoTracking()
                join membership in dbContext.Memberships.AsNoTracking()
                    on organization.Id equals membership.OrganizationId
                where organization.Id == organizationId &&
                      organization.Status == OrganizationState.Active &&
                      membership.Status == OrganizationMembershipState.Active &&
                      candidates.Contains(membership.SubjectId)
                select membership.SubjectId)
            .Distinct()
            .OrderBy(subjectId => subjectId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string NormalizeSubjectId(string candidate)
    {
        string normalized = candidate?.Trim() ?? string.Empty;
        return normalized.Length is >= 1 and <= OrganizationSubjectId.MaxLength &&
               normalized.All(character => !char.IsWhiteSpace(character) && !char.IsControl(character))
            ? normalized
            : throw new ArgumentException("Candidate subject ids must be valid organization subject ids.", nameof(candidate));
    }
}
