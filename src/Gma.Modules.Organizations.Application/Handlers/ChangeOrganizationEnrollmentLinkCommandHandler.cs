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

internal sealed class ChangeOrganizationEnrollmentLinkCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationEnrollmentTokenService tokens,
    IOptions<OrganizationsOptions> options,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ChangeOrganizationEnrollmentLinkCommand, OrganizationEnrollmentLinkMutationDto>
{
    public async Task<Result<OrganizationEnrollmentLinkMutationDto>> HandleAsync(
        ChangeOrganizationEnrollmentLinkCommand command,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, command.OrganizationId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkMutationDto>(owner.Error);
        }

        OrganizationEnrollmentLink? link = await organizations.GetEnrollmentLinkAsync(
            command.OrganizationId, command.EnrollmentLinkId, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return Result.Failure<OrganizationEnrollmentLinkMutationDto>(OrganizationApplicationErrors.EnrollmentLinkNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (command.Action == OrganizationEnrollmentLinkAction.Disable)
        {
            Result disabled = link.Disable(command.ExpectedVersion, command.ActorId, ids.NewId(), nowUtc);
            return disabled.IsSuccess
                ? Result.Success(new OrganizationEnrollmentLinkMutationDto(link.ToDto(nowUtc), null))
                : Result.Failure<OrganizationEnrollmentLinkMutationDto>(disabled.Error);
        }

        if (command.Action != OrganizationEnrollmentLinkAction.Rotate)
        {
            return Result.Failure<OrganizationEnrollmentLinkMutationDto>(OrganizationApplicationErrors.EnrollmentLinkNotFound);
        }

        Organization? organization = await organizations.GetOrganizationAsync(
            command.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationEnrollmentLinkMutationDto>(
                organization is null ? OrganizationApplicationErrors.OrganizationNotFound :
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        Result<int> lifetime = OrganizationEnrollmentHandlerSupport.ResolveLifetimeHours(
            command.ReplacementLifetimeHours, options);
        if (lifetime.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkMutationDto>(lifetime.Error);
        }

        Result rotated = link.Rotate(command.ExpectedVersion, command.ActorId, ids.NewId(), nowUtc);
        if (rotated.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkMutationDto>(rotated.Error);
        }

        IssuedOrganizationEnrollmentToken issued = tokens.Issue();
        Result<OrganizationEnrollmentLink> replacement = OrganizationEnrollmentLink.Create(
            ids.NewId(), link.OrganizationId, command.SubjectId, issued.Digest,
            nowUtc.AddHours(lifetime.Value), link.MaximumClaims, link.ApprovalMode,
            command.ActorId, ids.NewId(), nowUtc);
        if (replacement.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkMutationDto>(replacement.Error);
        }

        await organizations.AddEnrollmentLinkAsync(replacement.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationEnrollmentLinkMutationDto(
            replacement.Value.ToDto(nowUtc), issued.Secret));
    }
}
