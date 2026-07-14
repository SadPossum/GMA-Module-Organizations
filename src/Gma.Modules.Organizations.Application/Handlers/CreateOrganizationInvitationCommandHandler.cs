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
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.Extensions.Options;

internal sealed class CreateOrganizationInvitationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationInvitationTokenService tokens,
    IOptions<OrganizationsOptions> options,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<CreateOrganizationInvitationCommand, OrganizationInvitationIssuedDto>
{
    public async Task<Result<OrganizationInvitationIssuedDto>> HandleAsync(
        CreateOrganizationInvitationCommand command,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, command.OrganizationId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(owner.Error);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(command.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(
                organization is null ? OrganizationApplicationErrors.OrganizationNotFound :
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        Result<int> lifetime = OrganizationInvitationHandlerSupport.ResolveLifetimeHours(command.LifetimeHours, options);
        if (lifetime.IsFailure)
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(lifetime.Error);
        }

        IssuedOrganizationInvitationToken issued = tokens.Issue();
        DateTimeOffset nowUtc = clock.UtcNow;
        Result<OrganizationInvitation> invitation = OrganizationInvitation.Create(
            ids.NewId(), organization.Id, command.SubjectId, command.RecipientEmail,
            issued.Digest, nowUtc.AddHours(lifetime.Value), command.ActorId, ids.NewId(), nowUtc);
        if (invitation.IsFailure)
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(invitation.Error);
        }

        await organizations.AddInvitationAsync(invitation.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationInvitationIssuedDto(
            invitation.Value.ToDto(nowUtc), issued.Secret));
    }
}
