namespace Gma.Modules.Organizations.Persistence.Access;

using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationAccessDecisionReader(OrganizationsDbContext dbContext)
    : IOrganizationAccessDecisionReader
{
    public async Task<OrganizationAccessDecision> ReadAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
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
}
