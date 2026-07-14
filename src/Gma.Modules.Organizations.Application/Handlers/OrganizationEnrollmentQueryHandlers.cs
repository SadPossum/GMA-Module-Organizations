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

internal sealed class PreviewOrganizationEnrollmentLinkQueryHandler(
    IOrganizationRepository organizations,
    IOrganizationEnrollmentTokenService tokens,
    ISystemClock clock) : IQueryHandler<PreviewOrganizationEnrollmentLinkQuery, OrganizationEnrollmentPreviewDto>
{
    public async Task<Result<OrganizationEnrollmentPreviewDto>> HandleAsync(
        PreviewOrganizationEnrollmentLinkQuery query,
        CancellationToken cancellationToken)
    {
        if (!tokens.IsWellFormed(query.Token))
        {
            return Result.Failure<OrganizationEnrollmentPreviewDto>(
                OrganizationApplicationErrors.EnrollmentTokenInvalid);
        }

        OrganizationEnrollmentLink? link = await organizations.GetEnrollmentLinkByDigestAsync(
            tokens.ComputeDigest(query.Token), cancellationToken).ConfigureAwait(false);
        if (link is null || !tokens.Verify(query.Token, link.TokenDigest))
        {
            return Result.Failure<OrganizationEnrollmentPreviewDto>(
                OrganizationApplicationErrors.EnrollmentTokenInvalid);
        }

        Organization? organization = await organizations.GetOrganizationAsync(
            link.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            return Result.Failure<OrganizationEnrollmentPreviewDto>(
                OrganizationApplicationErrors.EnrollmentLinkNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        return Result.Success(new OrganizationEnrollmentPreviewDto(
            link.Id, organization.Id, organization.Name, organization.Slug,
            link.ExpiresAtUtc, Math.Max(0, link.MaximumClaims - link.ReservedClaims),
            OrganizationMappings.MapMode(link.ApprovalMode),
            OrganizationMappings.MapStatus(
                link.Status, link.ExpiresAtUtc, link.ReservedClaims, link.MaximumClaims, nowUtc)));
    }
}

internal sealed class ListOrganizationEnrollmentLinksQueryHandler(
    IOrganizationRepository organizations,
    ISystemClock clock) : IQueryHandler<ListOrganizationEnrollmentLinksQuery, OrganizationEnrollmentLinkListResponse>
{
    public async Task<Result<OrganizationEnrollmentLinkListResponse>> HandleAsync(
        ListOrganizationEnrollmentLinksQuery query,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, query.OrganizationId, query.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkListResponse>(owner.Error);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListEnrollmentLinksAsync(
            query.OrganizationId, page.Page, page.PageSize, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false));
    }
}

internal sealed class ListOrganizationJoinRequestsQueryHandler(IOrganizationRepository organizations)
    : IQueryHandler<ListOrganizationJoinRequestsQuery, OrganizationJoinRequestListResponse>
{
    public async Task<Result<OrganizationJoinRequestListResponse>> HandleAsync(
        ListOrganizationJoinRequestsQuery query,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, query.OrganizationId, query.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationJoinRequestListResponse>(owner.Error);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListPendingJoinRequestsAsync(
            query.OrganizationId, page.Page, page.PageSize, cancellationToken).ConfigureAwait(false));
    }
}
