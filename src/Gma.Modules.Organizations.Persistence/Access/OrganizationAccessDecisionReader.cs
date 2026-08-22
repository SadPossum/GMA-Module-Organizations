namespace Gma.Modules.Organizations.Persistence.Access;

using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class OrganizationAccessDecisionReader(OrganizationsDbContext dbContext)
    : IOrganizationAccessDecisionReader,
      IOrganizationAccessCandidateFilter,
      IOrganizationMembershipReader
{
    public async Task<OrganizationMembershipSnapshotDto?> FindAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        string normalizedSubject = NormalizeSubjectId(subjectId);
        var row = await (
                from organization in dbContext.Organizations.AsNoTracking()
                join membership in dbContext.Memberships.AsNoTracking()
                        .Where(candidate => candidate.SubjectId == normalizedSubject)
                    on organization.Id equals membership.OrganizationId into memberships
                from membership in memberships.DefaultIfEmpty()
                where organization.Id == organizationId
                select new
                {
                    OrganizationId = organization.Id,
                    OrganizationStatus = organization.Status,
                    MembershipId = membership == null ? (Guid?)null : membership.Id,
                    MembershipSubjectId = membership == null ? null : membership.SubjectId,
                    MembershipRole = membership == null
                        ? (DomainMembershipRole?)null
                        : membership.Role,
                    MembershipStatus = membership == null
                        ? (OrganizationMembershipState?)null
                        : membership.Status,
                    MembershipVersion = membership == null ? (long?)null : membership.Version,
                    MembershipJoinedAtUtc = membership == null
                        ? (DateTimeOffset?)null
                        : membership.JoinedAtUtc,
                    MembershipLastChangedAtUtc = membership == null
                        ? (DateTimeOffset?)null
                        : membership.LastChangedAtUtc
                })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        OrganizationMembershipDto? membershipDto = row.MembershipId is { } membershipId &&
            row.MembershipSubjectId is { } membershipSubjectId &&
            row.MembershipRole is { } membershipRole &&
            row.MembershipStatus is { } membershipStatus &&
            row.MembershipVersion is { } membershipVersion &&
            row.MembershipJoinedAtUtc is { } membershipJoinedAtUtc &&
            row.MembershipLastChangedAtUtc is { } membershipLastChangedAtUtc
                ? new OrganizationMembershipDto(
                    membershipId,
                    row.OrganizationId,
                    membershipSubjectId,
                    OrganizationMappings.MapRole(membershipRole),
                    OrganizationMappings.MapStatus(membershipStatus),
                    membershipVersion,
                    membershipJoinedAtUtc,
                    membershipLastChangedAtUtc)
                : null;
        return new OrganizationMembershipSnapshotDto(
            row.OrganizationId,
            OrganizationMappings.MapStatus(row.OrganizationStatus),
            membershipDto);
    }

    public async Task<OrganizationAccessDecision> ReadAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        OrganizationMembershipSnapshotDto? snapshot = await this.FindAsync(
            organizationId,
            subjectId,
            cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return OrganizationAccessDecision.OrganizationNotFound;
        }

        if (snapshot.OrganizationStatus != OrganizationStatus.Active)
        {
            return OrganizationAccessDecision.OrganizationInactive;
        }

        return snapshot.Membership?.Status switch
        {
            null => OrganizationAccessDecision.MembershipNotFound,
            OrganizationMembershipStatus.Active => OrganizationAccessDecision.Allowed,
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
