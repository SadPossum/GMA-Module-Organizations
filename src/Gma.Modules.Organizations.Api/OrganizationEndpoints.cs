namespace Gma.Modules.Organizations.Api;

using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Security;
using Gma.Modules.Organizations.Api.Requests;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class OrganizationEndpoints
{
    public static void Map(
        IEndpointRouteBuilder endpoints,
        string moduleName,
        AuthenticationAssuranceRequirement? governanceAssurance)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/organizations")
            .WithModuleName(moduleName)
            .WithTags("Organizations")
            .RequireAuthorization();

        MapCatalog(group, governanceAssurance);
        MapLifecycle(group, governanceAssurance);
        MapMemberships(group, governanceAssurance);
        MapInvitations(group, governanceAssurance);
        OrganizationEnrollmentEndpoints.MapOwnerOperations(group, governanceAssurance);
        MapInvitationAcceptance(endpoints, moduleName);
        OrganizationEnrollmentEndpoints.MapClaimOperations(endpoints, moduleName);
    }

    private static void MapCatalog(
        RouteGroupBuilder group,
        AuthenticationAssuranceRequirement? governanceAssurance)
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

        RouteHandlerBuilder createOrganization = group.MapPost("", async (CreateOrganizationRequest request, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new CreateOrganizationCommand(
                request.OperationId,
                request.Name,
                request.Slug,
                subjectId,
                OrganizationEndpointSupport.Actor(subjectId)), token)
                .ConfigureAwait(false)).ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationMembershipSummaryDto>(StatusCodes.Status200OK);
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(createOrganization, governanceAssurance);

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

        RouteHandlerBuilder updateOrganization = group.MapPut("/{organizationId:guid}", async (Guid organizationId,
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
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(updateOrganization, governanceAssurance);
    }

    private static void MapLifecycle(
        RouteGroupBuilder group,
        AuthenticationAssuranceRequirement? governanceAssurance)
    {
        MapLifecycleAction(group, "suspend", OrganizationLifecycleAction.Suspend, governanceAssurance);
        MapLifecycleAction(group, "reactivate", OrganizationLifecycleAction.Reactivate, governanceAssurance);
        MapLifecycleAction(group, "archive", OrganizationLifecycleAction.Archive, governanceAssurance);
    }

    private static void MapLifecycleAction(
        RouteGroupBuilder group,
        string route,
        OrganizationLifecycleAction action,
        AuthenticationAssuranceRequirement? governanceAssurance)
    {
        RouteHandlerBuilder endpoint = group.MapPost($"/{{organizationId:guid}}/{route}", async (Guid organizationId,
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
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(endpoint, governanceAssurance);
    }

    private static void MapMemberships(
        RouteGroupBuilder group,
        AuthenticationAssuranceRequirement? governanceAssurance)
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

        MapMembershipAction(group, "suspend", OrganizationMembershipAction.Suspend, governanceAssurance);
        MapMembershipAction(group, "resume", OrganizationMembershipAction.Resume, governanceAssurance);
        MapMembershipAction(group, "remove", OrganizationMembershipAction.Remove, governanceAssurance);

        RouteHandlerBuilder transferOwnership = group.MapPost("/{organizationId:guid}/ownership/transfer", async (Guid organizationId,
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
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(transferOwnership, governanceAssurance);
    }

    private static void MapMembershipAction(
        RouteGroupBuilder group,
        string route,
        OrganizationMembershipAction action,
        AuthenticationAssuranceRequirement? governanceAssurance)
    {
        RouteHandlerBuilder endpoint = group.MapPost($"/{{organizationId:guid}}/members/{route}", async (Guid organizationId,
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
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(endpoint, governanceAssurance);
    }

    private static void MapInvitations(
        RouteGroupBuilder group,
        AuthenticationAssuranceRequirement? governanceAssurance)
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

        RouteHandlerBuilder createInvitation = group.MapPost("/{organizationId:guid}/invitations", async (Guid organizationId,
            CreateOrganizationInvitationRequest request, HttpContext context,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> result =
                await dispatcher.SendAsync(
                    new IssueOrganizationInvitationCommand(
                        new OrganizationInvitationIssuanceRequest(
                            request.SourceId,
                            organizationId,
                            request.RecipientEmail,
                            request.LifetimeHours,
                            subjectId,
                            OrganizationEndpointSupport.Actor(subjectId))),
                    token).ConfigureAwait(false);
            return OrganizationEndpointSupport.MapInvitationIssuance(result)
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationInvitationIssuanceDto>(StatusCodes.Status200OK);
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(createInvitation, governanceAssurance);

        RouteHandlerBuilder revokeInvitation = group.MapPost("/{organizationId:guid}/invitations/{invitationId:guid}/revoke", async (
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
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(revokeInvitation, governanceAssurance);

        RouteHandlerBuilder reissueInvitation = group.MapPost("/{organizationId:guid}/invitations/{invitationId:guid}/reissue", async (
            Guid organizationId, Guid invitationId, ReissueOrganizationInvitationRequest request,
            HttpContext context, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            OrganizationEndpointSupport.SetNoStoreHeaders(context);
            if (!OrganizationEndpointSupport.TryGetSubject(context, out string subjectId))
            {
                return Results.Unauthorized();
            }

            Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> result =
                await dispatcher.SendAsync(new ReissueOrganizationInvitationCommand(
                    organizationId,
                    invitationId,
                    request.ReplacementSourceId,
                    request.ExpectedVersion,
                    request.LifetimeHours,
                    subjectId,
                    OrganizationEndpointSupport.Actor(subjectId)), token).ConfigureAwait(false);
            return OrganizationEndpointSupport.MapInvitationIssuance(result)
                .ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
        }).Produces<OrganizationInvitationIssuanceDto>(StatusCodes.Status200OK);
        OrganizationEndpointSupport.RequireAssuranceWhenConfigured(reissueInvitation, governanceAssurance);
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
