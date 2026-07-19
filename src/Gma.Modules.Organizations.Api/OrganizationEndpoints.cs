namespace Gma.Modules.Organizations.Api;

using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Modules.Organizations.Api.Requests;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class OrganizationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/organizations")
            .WithModuleName(moduleName)
            .WithTags("Organizations")
            .RequireAuthorization();

        MapCatalog(group);
        MapLifecycle(group);
        MapMemberships(group);
        MapInvitations(group);
        OrganizationEnrollmentEndpoints.MapOwnerOperations(group);
        MapInvitationAcceptance(endpoints, moduleName);
        OrganizationEnrollmentEndpoints.MapClaimOperations(endpoints, moduleName);
    }

    private static void MapCatalog(RouteGroupBuilder group)
    {
        group.MapGet("", async (int? page, int? pageSize, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.QueryAsync(new ListMyOrganizationsQuery(
                subjectId, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationListResponse>(StatusCodes.Status200OK);

        group.MapPost("", async (CreateOrganizationRequest request, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new CreateOrganizationCommand(
                request.Name, request.Slug, subjectId, OrganizationEndpointSupport.Actor(subjectId)), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationMembershipSummaryDto>(StatusCodes.Status200OK);

        group.MapGet("/{organizationId:guid}", async (Guid organizationId, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.QueryAsync(new GetOrganizationQuery(organizationId, subjectId), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationMembershipSummaryDto>(StatusCodes.Status200OK);

        group.MapPut("/{organizationId:guid}", async (Guid organizationId,
            UpdateOrganizationRequest request, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new UpdateOrganizationCommand(
                organizationId, request.Name, request.Slug, request.ExpectedVersion,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationDto>(StatusCodes.Status200OK);
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        MapLifecycleAction(group, "suspend", OrganizationLifecycleAction.Suspend);
        MapLifecycleAction(group, "reactivate", OrganizationLifecycleAction.Reactivate);
        MapLifecycleAction(group, "archive", OrganizationLifecycleAction.Archive);
    }

    private static void MapLifecycleAction(
        RouteGroupBuilder group,
        string route,
        OrganizationLifecycleAction action)
    {
        group.MapPost($"/{{organizationId:guid}}/{route}", async (Guid organizationId,
            OrganizationLifecycleRequest request, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new ChangeOrganizationLifecycleCommand(
                organizationId, action, request.ExpectedVersion,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationDto>(StatusCodes.Status200OK);
    }

    private static void MapMemberships(RouteGroupBuilder group)
    {
        group.MapGet("/{organizationId:guid}/members", async (Guid organizationId,
            int? page, int? pageSize, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.QueryAsync(new ListOrganizationMembersQuery(
                organizationId, subjectId, page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationMemberListResponse>(StatusCodes.Status200OK);

        MapMembershipAction(group, "suspend", OrganizationMembershipAction.Suspend);
        MapMembershipAction(group, "resume", OrganizationMembershipAction.Resume);
        MapMembershipAction(group, "remove", OrganizationMembershipAction.Remove);

        group.MapPost("/{organizationId:guid}/ownership/transfer", async (Guid organizationId,
            TransferOrganizationOwnershipRequest request, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new TransferOrganizationOwnershipCommand(
                organizationId, request.TargetSubjectId, request.ExpectedOrganizationVersion,
                request.ExpectedCurrentOwnerVersion, request.ExpectedTargetVersion,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationMembershipDto>(StatusCodes.Status200OK);
    }

    private static void MapMembershipAction(
        RouteGroupBuilder group,
        string route,
        OrganizationMembershipAction action)
    {
        group.MapPost($"/{{organizationId:guid}}/members/{route}", async (Guid organizationId,
            OrganizationMembershipLifecycleRequest request, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new ChangeOrganizationMembershipCommand(
                organizationId, request.TargetSubjectId, action,
                request.ExpectedOrganizationVersion, request.ExpectedMembershipVersion,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationMembershipDto>(StatusCodes.Status200OK);
    }

    private static void MapInvitations(RouteGroupBuilder group)
    {
        group.MapGet("/{organizationId:guid}/invitations", async (Guid organizationId,
            int? page, int? pageSize, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.QueryAsync(new ListOrganizationInvitationsQuery(
                organizationId, subjectId, page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationInvitationListResponse>(StatusCodes.Status200OK);

        group.MapPost("/{organizationId:guid}/invitations", async (Guid organizationId,
            CreateOrganizationInvitationRequest request, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new CreateOrganizationInvitationCommand(
                organizationId, request.RecipientEmail, request.LifetimeHours,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationInvitationIssuedDto>(StatusCodes.Status200OK);

        group.MapPost("/{organizationId:guid}/invitations/{invitationId:guid}/revoke", async (
            Guid organizationId, Guid invitationId, RevokeOrganizationInvitationRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new RevokeOrganizationInvitationCommand(
                organizationId, invitationId, request.ExpectedVersion,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationInvitationDto>(StatusCodes.Status200OK);

        group.MapPost("/{organizationId:guid}/invitations/{invitationId:guid}/reissue", async (
            Guid organizationId, Guid invitationId, ReissueOrganizationInvitationRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new ReissueOrganizationInvitationCommand(
                organizationId, invitationId, request.ExpectedVersion, request.LifetimeHours,
                subjectId, OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false))
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationInvitationIssuedDto>(StatusCodes.Status200OK);
    }

    private static void MapInvitationAcceptance(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder invitations = endpoints.MapGroup("/api/organization-invitations")
            .WithModuleName(moduleName)
            .WithTags("Organization Invitations");

        invitations.MapPost("/preview", async (PreviewOrganizationInvitationRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            return (await dispatcher.QueryAsync(
                new PreviewOrganizationInvitationQuery(request.Token), cancellationToken)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        })
            .Produces<OrganizationInvitationPreviewDto>(StatusCodes.Status200OK);

        invitations.MapPost("/accept", async (AcceptOrganizationInvitationRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new AcceptOrganizationInvitationCommand(
                request.Token, subjectId, OrganizationEndpointSupport.Actor(subjectId)), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationInvitationAcceptanceDto>(StatusCodes.Status200OK)
            .RequireAuthorization();
    }
}
