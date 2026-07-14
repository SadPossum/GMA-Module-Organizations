namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

internal sealed class RevokeOrganizationInvitationCommandHandler(
    IOrganizationRepository organizations,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<RevokeOrganizationInvitationCommand, OrganizationInvitationDto>
{
    public async Task<Result<OrganizationInvitationDto>> HandleAsync(
        RevokeOrganizationInvitationCommand command,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, command.OrganizationId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationInvitationDto>(owner.Error);
        }

        OrganizationInvitation? invitation = await organizations.GetInvitationAsync(
            command.OrganizationId, command.InvitationId, cancellationToken).ConfigureAwait(false);
        if (invitation is null)
        {
            return Result.Failure<OrganizationInvitationDto>(OrganizationApplicationErrors.InvitationNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result revoked = invitation.Revoke(
            command.ExpectedVersion, command.ActorId, ids.NewId(), nowUtc);
        return revoked.IsSuccess
            ? Result.Success(invitation.ToDto(nowUtc))
            : Result.Failure<OrganizationInvitationDto>(revoked.Error);
    }
}
