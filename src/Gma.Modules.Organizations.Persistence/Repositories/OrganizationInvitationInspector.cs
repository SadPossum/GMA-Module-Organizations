namespace Gma.Modules.Organizations.Persistence.Repositories;

using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationInvitationInspector(
    OrganizationsDbContext dbContext,
    ISystemClock clock) : IOrganizationInvitationInspector
{
    public async Task<OrganizationInvitationStatus?> FindStatusAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(invitationId, Guid.Empty);

        DateTimeOffset nowUtc = clock.UtcNow;
        InvitationStatusSnapshot? invitation = await dbContext.Invitations
            .AsNoTracking()
            .Where(candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.Id == invitationId)
            .Select(candidate => new InvitationStatusSnapshot(
                candidate.Status,
                candidate.ExpiresAtUtc))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return invitation is null
            ? null
            : OrganizationMappings.MapStatus(
                invitation.Status,
                invitation.ExpiresAtUtc,
                nowUtc);
    }

    private sealed record InvitationStatusSnapshot(
        OrganizationInvitationState Status,
        DateTimeOffset ExpiresAtUtc);
}
