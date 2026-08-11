namespace Gma.Modules.Organizations.Persistence.Access;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class OrganizationMembershipInspector(OrganizationsDbContext dbContext)
    : IOrganizationMembershipInspector
{
    public async Task<OrganizationMembershipSnapshot?> FindAsync(
        Guid organizationId,
        Guid membershipId,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(membershipId, Guid.Empty);
        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(subjectId);
        if (subject.IsFailure)
        {
            throw new ArgumentException(subject.Error.Message, nameof(subjectId));
        }

        MembershipSnapshot? membership = await (
                from organization in dbContext.Organizations.AsNoTracking()
                join candidate in dbContext.Memberships.AsNoTracking()
                    on organization.Id equals candidate.OrganizationId
                join scopeState in dbContext.OrganizationScopeStates.AsNoTracking()
                    on organization.Id equals scopeState.OrganizationId into scopeStates
                from scopeState in scopeStates.DefaultIfEmpty()
                where organization.Id == organizationId &&
                      candidate.Id == membershipId &&
                      candidate.SubjectId == subject.Value.Value
                select new MembershipSnapshot(
                    organization.Id,
                    candidate.Id,
                    organization.Status,
                    scopeState == null ? null : scopeState.IsClosed,
                    scopeState == null ? 0 : scopeState.Version,
                    candidate.Role,
                    candidate.Status,
                    candidate.Version))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return membership is null
            ? null
            : new OrganizationMembershipSnapshot(
                membership.OrganizationId,
                membership.MembershipId,
                OrganizationMappings.MapStatus(membership.OrganizationStatus),
                membership.ScopeIsClosed is true
                    ? OrganizationScopeStatus.Closed
                    : OrganizationScopeStatus.Open,
                membership.ScopeRevision,
                OrganizationMappings.MapRole(membership.Role),
                OrganizationMappings.MapStatus(membership.MembershipStatus),
                membership.MembershipVersion);
    }

    private sealed record MembershipSnapshot(
        Guid OrganizationId,
        Guid MembershipId,
        OrganizationState OrganizationStatus,
        bool? ScopeIsClosed,
        long ScopeRevision,
        DomainMembershipRole Role,
        OrganizationMembershipState MembershipStatus,
        long MembershipVersion);
}
