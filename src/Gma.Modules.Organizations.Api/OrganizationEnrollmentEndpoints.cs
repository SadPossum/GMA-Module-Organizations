namespace Gma.Modules.Organizations.Api;

using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Security;
using Gma.Modules.Organizations.Api.Requests;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class OrganizationEnrollmentEndpoints
{
    public static void MapOwnerOperations(
        RouteGroupBuilder organizations,
        AuthenticationAssuranceRequirement? governanceAssurance)
    {
        organizations.MapGet("/{organizationId:guid}/enrollment-links", async (
            Guid organizationId, int? page, int? pageSize, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.QueryAsync(new ListOrganizationEnrollmentLinksQuery(
                organizationId, subjectId, page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationEnrollmentLinkListResponse>(StatusCodes.Status200OK);

        RouteHandlerBuilder createLink = organizations.MapPost("/{organizationId:guid}/enrollment-links", async (
            Guid organizationId, CreateOrganizationEnrollmentLinkRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new CreateOrganizationEnrollmentLinkCommand(
                organizationId, request.LifetimeHours, request.MaximumClaims, request.ApprovalMode,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationEnrollmentLinkIssuedDto>(StatusCodes.Status200OK);
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(createLink, governanceAssurance);

        MapLinkAction(organizations, "disable", OrganizationEnrollmentLinkAction.Disable, governanceAssurance);
        MapLinkAction(organizations, "rotate", OrganizationEnrollmentLinkAction.Rotate, governanceAssurance);

        organizations.MapGet("/{organizationId:guid}/join-requests", async (
            Guid organizationId, int? page, int? pageSize, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.QueryAsync(new ListOrganizationJoinRequestsQuery(
                organizationId, subjectId, page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationJoinRequestListResponse>(StatusCodes.Status200OK);

        MapJoinRequestDecision(organizations, "approve", OrganizationJoinRequestDecision.Approve, governanceAssurance);
        MapJoinRequestDecision(organizations, "reject", OrganizationJoinRequestDecision.Reject, governanceAssurance);
    }

    public static void MapClaimOperations(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder enrollment = endpoints.MapGroup("/api/organization-enrollment")
            .WithModuleName(moduleName)
            .WithTags("Organization Enrollment");

        enrollment.MapPost("/preview", async (PreviewOrganizationEnrollmentLinkRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            return (await dispatcher.QueryAsync(
                new PreviewOrganizationEnrollmentLinkQuery(request.Token), cancellationToken)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        })
            .Produces<OrganizationEnrollmentPreviewDto>(StatusCodes.Status200OK);

        enrollment.MapPost("/claim", async (ClaimOrganizationEnrollmentLinkRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new ClaimOrganizationEnrollmentLinkCommand(
                request.Token, subjectId, OrganizationEndpointSupport.Actor(subjectId)), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationEnrollmentOutcomeDto>(StatusCodes.Status200OK)
            .RequireAuthorization();
    }

    private static void MapLinkAction(
        RouteGroupBuilder organizations,
        string route,
        OrganizationEnrollmentLinkAction action,
        AuthenticationAssuranceRequirement? governanceAssurance)
    {
        RouteHandlerBuilder endpoint = organizations.MapPost($"/{{organizationId:guid}}/enrollment-links/{{enrollmentLinkId:guid}}/{route}",
            async (Guid organizationId, Guid enrollmentLinkId,
                ChangeOrganizationEnrollmentLinkRequest request, HttpContext context,
                IRequestDispatcher dispatcher, CancellationToken token) =>
            {
                OrganizationEndpointSupport.SetNoStoreHeaders(context);
                if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
                {
                    return Results.Unauthorized();
                }

                return (await dispatcher.SendAsync(new ChangeOrganizationEnrollmentLinkCommand(
                    organizationId, enrollmentLinkId, action, request.ExpectedVersion,
                    request.ReplacementLifetimeHours, subjectId,
                    OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                    .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
            }).Produces<OrganizationEnrollmentLinkMutationDto>(StatusCodes.Status200OK);
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(endpoint, governanceAssurance);
    }

    private static void MapJoinRequestDecision(
        RouteGroupBuilder organizations,
        string route,
        OrganizationJoinRequestDecision decision,
        AuthenticationAssuranceRequirement? governanceAssurance)
    {
        RouteHandlerBuilder endpoint = organizations.MapPost($"/{{organizationId:guid}}/join-requests/{{claimId:guid}}/{route}",
            async (Guid organizationId, Guid claimId,
                ResolveOrganizationJoinRequestRequest request, HttpContext context,
                IRequestDispatcher dispatcher, CancellationToken token) =>
            {
                if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
                {
                    return Results.Unauthorized();
                }

                return (await dispatcher.SendAsync(new ResolveOrganizationJoinRequestCommand(
                    organizationId, claimId, decision, request.ExpectedVersion,
                    subjectId, OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                    .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
            }).Produces<OrganizationEnrollmentOutcomeDto>(StatusCodes.Status200OK);
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(endpoint, governanceAssurance);
    }
}
