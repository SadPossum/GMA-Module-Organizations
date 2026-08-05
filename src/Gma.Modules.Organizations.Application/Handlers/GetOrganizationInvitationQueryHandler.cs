namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

internal sealed class GetOrganizationInvitationQueryHandler(
    IOrganizationRepository organizations,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    ISystemClock clock) : IQueryHandler<GetOrganizationInvitationQuery, OrganizationInvitationDto>
{
    public async Task<Result<OrganizationInvitationDto>> HandleAsync(
        GetOrganizationInvitationQuery query,
        CancellationToken cancellationToken)
    {
        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.ReadInvitations,
                query.OrganizationId,
                query.SubjectId,
                query.InvitationId),
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Result.Failure<OrganizationInvitationDto>(authorized.Error);
        }

        OrganizationInvitation? invitation = await organizations.GetInvitationAsync(
            query.OrganizationId, query.InvitationId, cancellationToken).ConfigureAwait(false);
        return invitation is null
            ? Result.Failure<OrganizationInvitationDto>(OrganizationApplicationErrors.InvitationNotFound)
            : Result.Success(invitation.ToDto(clock.UtcNow));
    }
}
