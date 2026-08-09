namespace Gma.Modules.Organizations.Persistence.Repositories;

using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationJoinSourceIssuanceCoordinator(
    OrganizationsDbContext dbContext) : IOrganizationJoinSourceIssuanceCoordinator
{
    public async Task<OrganizationInvitation?> AcquireInvitationAsync(
        Guid organizationId,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        await this.AcquireAsync(sourceId, cancellationToken).ConfigureAwait(false);
        return await dbContext.Invitations.SingleOrDefaultAsync(
            invitation => invitation.OrganizationId == organizationId &&
                          invitation.Id == sourceId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OrganizationEnrollmentLink?> AcquireEnrollmentLinkAsync(
        Guid organizationId,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        await this.AcquireAsync(sourceId, cancellationToken).ConfigureAwait(false);
        return await dbContext.EnrollmentLinks.SingleOrDefaultAsync(
            link => link.OrganizationId == organizationId && link.Id == sourceId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task AcquireReplacementAsync(
        Guid sourceId,
        Guid replacementSourceId,
        CancellationToken cancellationToken)
    {
        Guid first = sourceId.CompareTo(replacementSourceId) <= 0
            ? sourceId
            : replacementSourceId;
        Guid second = first == sourceId ? replacementSourceId : sourceId;
        await this.AcquireAsync(first, cancellationToken).ConfigureAwait(false);
        if (second != first)
        {
            await this.AcquireAsync(second, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task AcquireAsync(Guid sourceId, CancellationToken cancellationToken) =>
        EfTransactionKeyLock.AcquireAsync(
            dbContext,
            $"gma:organizations:join-source:{sourceId:N}",
            cancellationToken);
}
