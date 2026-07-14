namespace Gma.Modules.Organizations.Api;

using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Modules.Organizations.Api.Requests;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class OrganizationEnrollmentEndpoints
{
    public static void MapOwnerOperations(RouteGroupBuilder organizations)
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
        });

        organizations.MapPost("/{organizationId:guid}/enrollment-links", async (
            Guid organizationId, CreateOrganizationEnrollmentLinkRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new CreateOrganizationEnrollmentLinkCommand(
                organizationId, request.LifetimeHours, request.MaximumClaims, request.ApprovalMode,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        });

        MapLinkAction(organizations, "disable", OrganizationEnrollmentLinkAction.Disable);
        MapLinkAction(organizations, "rotate", OrganizationEnrollmentLinkAction.Rotate);

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
        });

        MapJoinRequestDecision(organizations, "approve", OrganizationJoinRequestDecision.Approve);
        MapJoinRequestDecision(organizations, "reject", OrganizationJoinRequestDecision.Reject);
    }

    public static void MapClaimOperations(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder enrollment = endpoints.MapGroup("/api/organization-enrollment")
            .WithModuleName(moduleName)
            .WithTags("Organization Enrollment");

        enrollment.MapPost("/preview", async (PreviewOrganizationEnrollmentLinkRequest request,
            IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new PreviewOrganizationEnrollmentLinkQuery(request.Token), cancellationToken)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes));

        enrollment.MapPost("/claim", async (ClaimOrganizationEnrollmentLinkRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new ClaimOrganizationEnrollmentLinkCommand(
                request.Token, subjectId, OrganizationEndpointSupport.Actor(subjectId)), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).RequireAuthorization();
    }

    private static void MapLinkAction(
        RouteGroupBuilder organizations,
        string route,
        OrganizationEnrollmentLinkAction action)
    {
        organizations.MapPost($"/{{organizationId:guid}}/enrollment-links/{{enrollmentLinkId:guid}}/{route}",
            async (Guid organizationId, Guid enrollmentLinkId,
                ChangeOrganizationEnrollmentLinkRequest request, HttpContext context,
                IRequestDispatcher dispatcher, CancellationToken token) =>
            {
                if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
                {
                    return Results.Unauthorized();
                }

                return (await dispatcher.SendAsync(new ChangeOrganizationEnrollmentLinkCommand(
                    organizationId, enrollmentLinkId, action, request.ExpectedVersion,
                    request.ReplacementLifetimeHours, subjectId,
                    OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                    .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
            });
    }

    private static void MapJoinRequestDecision(
        RouteGroupBuilder organizations,
        string route,
        OrganizationJoinRequestDecision decision)
    {
        organizations.MapPost($"/{{organizationId:guid}}/join-requests/{{claimId:guid}}/{route}",
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
            });
    }
}
