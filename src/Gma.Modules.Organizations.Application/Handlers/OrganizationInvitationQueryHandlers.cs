namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

internal sealed class PreviewOrganizationInvitationQueryHandler(
    IOrganizationRepository organizations,
    IOrganizationInvitationTokenService tokens,
    ISystemClock clock) : IQueryHandler<PreviewOrganizationInvitationQuery, OrganizationInvitationPreviewDto>
{
    public async Task<Result<OrganizationInvitationPreviewDto>> HandleAsync(
        PreviewOrganizationInvitationQuery query,
        CancellationToken cancellationToken)
    {
        if (!tokens.IsWellFormed(query.Token))
        {
            return Result.Failure<OrganizationInvitationPreviewDto>(
                OrganizationApplicationErrors.InvitationTokenInvalid);
        }

        OrganizationInvitation? invitation = await organizations.GetInvitationByDigestAsync(
            tokens.ComputeDigest(query.Token), cancellationToken).ConfigureAwait(false);
        if (invitation is null || !tokens.Verify(query.Token, invitation.TokenDigest))
        {
            return Result.Failure<OrganizationInvitationPreviewDto>(
                OrganizationApplicationErrors.InvitationTokenInvalid);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(invitation.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is null)
        {
            return Result.Failure<OrganizationInvitationPreviewDto>(OrganizationApplicationErrors.InvitationNotFound);
        }

        return Result.Success(new OrganizationInvitationPreviewDto(
            invitation.Id,
            organization.Id,
            organization.Name,
            organization.Slug,
            invitation.RecipientEmail is not null,
            invitation.ExpiresAtUtc,
            OrganizationMappings.MapStatus(invitation.Status, invitation.ExpiresAtUtc, clock.UtcNow)));
    }
}

internal sealed class ListOrganizationInvitationsQueryHandler(
    IOrganizationRepository organizations,
    ISystemClock clock) : IQueryHandler<ListOrganizationInvitationsQuery, OrganizationInvitationListResponse>
{
    public async Task<Result<OrganizationInvitationListResponse>> HandleAsync(
        ListOrganizationInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, query.OrganizationId, query.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationInvitationListResponse>(owner.Error);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListInvitationsAsync(
            query.OrganizationId, page.Page, page.PageSize, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false));
    }
}
