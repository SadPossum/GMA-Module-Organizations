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
using DomainApprovalMode = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;

internal sealed class CreateOrganizationEnrollmentLinkCommandHandler(
    IOrganizationRepository organizations,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    IOrganizationEnrollmentTokenService tokens,
    IOptions<OrganizationsOptions> options,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<CreateOrganizationEnrollmentLinkCommand, OrganizationEnrollmentLinkIssuedDto>
{
    public async Task<Result<OrganizationEnrollmentLinkIssuedDto>> HandleAsync(
        CreateOrganizationEnrollmentLinkCommand command,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, command.OrganizationId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkIssuedDto>(owner.Error);
        }

        Organization? organization = await organizations.GetOrganizationAsync(
            command.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationEnrollmentLinkIssuedDto>(
                organization is null ? OrganizationApplicationErrors.OrganizationNotFound :
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        Result<int> lifetime = OrganizationEnrollmentHandlerSupport.ResolveLifetimeHours(command.LifetimeHours, options);
        Result<int> claims = OrganizationEnrollmentHandlerSupport.ValidateMaximumClaims(command.MaximumClaims, options);
        Result<DomainApprovalMode> mode = OrganizationEnrollmentHandlerSupport.MapMode(command.ApprovalMode);
        if (lifetime.IsFailure || claims.IsFailure || mode.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkIssuedDto>(
                lifetime.IsFailure ? lifetime.Error : claims.IsFailure ? claims.Error : mode.Error);
        }

        Result admitted = await mutationAdmission.AuthorizeAsync(
            new OrganizationMutationAdmissionContext(
                OrganizationMutationAdmissionOperation.IssueEnrollmentLink,
                command.OrganizationId,
                command.SubjectId),
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkIssuedDto>(admitted.Error);
        }

        IssuedOrganizationEnrollmentToken issued = tokens.Issue();
        DateTimeOffset nowUtc = clock.UtcNow;
        Result<OrganizationEnrollmentLink> link = OrganizationEnrollmentLink.Create(
            ids.NewId(), organization.Id, command.SubjectId, issued.Digest,
            nowUtc.AddHours(lifetime.Value), claims.Value, mode.Value,
            command.ActorId, ids.NewId(), nowUtc);
        if (link.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkIssuedDto>(link.Error);
        }

        await organizations.AddEnrollmentLinkAsync(link.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationEnrollmentLinkIssuedDto(link.Value.ToDto(nowUtc), issued.Secret));
    }
}
