namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Mapping;
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
