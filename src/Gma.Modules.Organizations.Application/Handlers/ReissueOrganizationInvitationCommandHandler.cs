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

internal sealed class ReissueOrganizationInvitationCommandHandler(
    IOrganizationRepository organizations,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    IOrganizationInvitationTokenService tokens,
    IOptions<OrganizationsOptions> options,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ReissueOrganizationInvitationCommand, OrganizationInvitationIssuedDto>
{
    public async Task<Result<OrganizationInvitationIssuedDto>> HandleAsync(
        ReissueOrganizationInvitationCommand command,
        CancellationToken cancellationToken)
    {
        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.ReissueInvitation,
                command.OrganizationId,
                command.SubjectId,
                command.InvitationId),
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(authorized.Error);
        }

        OrganizationInvitation? existing = await organizations.GetInvitationAsync(
            command.OrganizationId, command.InvitationId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(OrganizationApplicationErrors.InvitationNotFound);
        }

        Organization? organization = await organizations.GetOrganizationAsync(
            command.OrganizationId, cancellationToken).ConfigureAwait(false);
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

        Result admitted = await mutationAdmission.AuthorizeAsync(
            new OrganizationMutationAdmissionContext(
                OrganizationMutationAdmissionOperation.ReissueInvitation,
                command.OrganizationId,
                command.SubjectId,
                command.InvitationId),
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(admitted.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result superseded = existing.Supersede(
            command.ExpectedVersion, command.ActorId, ids.NewId(), nowUtc);
        if (superseded.IsFailure)
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(superseded.Error);
        }

        IssuedOrganizationInvitationToken issued = tokens.Issue();
        Result<OrganizationInvitation> replacement = OrganizationInvitation.Create(
            ids.NewId(), existing.OrganizationId, command.SubjectId, existing.RecipientEmail,
            issued.Digest, nowUtc.AddHours(lifetime.Value), command.ActorId, ids.NewId(), nowUtc);
        if (replacement.IsFailure)
        {
            return Result.Failure<OrganizationInvitationIssuedDto>(replacement.Error);
        }

        await organizations.AddInvitationAsync(replacement.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationInvitationIssuedDto(
            replacement.Value.ToDto(nowUtc), issued.Secret));
    }
}
