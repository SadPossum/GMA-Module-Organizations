namespace Gma.Modules.Organizations.AdminApi;

using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Admin.Contracts;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class OrganizationsAdminApiModule : IAdminApiModule
{
    public string Name => OrganizationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddOrganizationsApplication(builder.Configuration);
        builder.AddOrganizationsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/organizations")
            .WithModuleName(this.Name)
            .WithTags("Organizations Admin")
            .RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                context,
                AdminOperation.Create(OrganizationsAdminOperationNames.CatalogList, OrganizationsAdminPermissions.Read),
                requireTenant: false,
                token => dispatcher.QueryAsync(new ListOrganizationCatalogForAdministrationQuery(
                    page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapGet("/{organizationId:guid}/members", async (
            Guid organizationId, int? page, int? pageSize, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                context,
                AdminOperation.Create(OrganizationsAdminOperationNames.MembersList, OrganizationsAdminPermissions.Read),
                requireTenant: false,
                token => dispatcher.QueryAsync(new ListOrganizationMembersForAdministrationQuery(
                    organizationId, page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize), token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        MapLifecycle(group, "suspend", OrganizationLifecycleAction.Suspend, requiresConfirmation: true);
        MapLifecycle(group, "reactivate", OrganizationLifecycleAction.Reactivate, requiresConfirmation: false);
        MapLifecycle(group, "archive", OrganizationLifecycleAction.Archive, requiresConfirmation: true);

        group.MapPost("/{organizationId:guid}/owners/ensure", async (
            Guid organizationId, EnsureOrganizationOwnerAdminRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            IAdminActorContext actorContext, CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                context,
                AdminOperation.Create(OrganizationsAdminOperationNames.OwnerEnsure, OrganizationsAdminPermissions.Manage),
                requireTenant: false,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new EnsureOrganizationOwnerForAdministrationCommand(
                        organizationId, request.SubjectId, request.ExpectedOrganizationVersion,
                        request.ExpectedMembershipVersion, ResolveActor(actorContext)), token)
                    : Task.FromResult(Result.Failure<OrganizationMembershipSummaryDto>(
                        AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static void MapLifecycle(
        RouteGroupBuilder group,
        string route,
        OrganizationLifecycleAction action,
        bool requiresConfirmation)
    {
        group.MapPost($"/{{organizationId:guid}}/{route}", async (
            Guid organizationId, ChangeOrganizationLifecycleAdminRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            IAdminActorContext actorContext, CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                context,
                AdminOperation.Create(OrganizationsAdminOperationNames.LifecycleChange, OrganizationsAdminPermissions.Manage),
                requireTenant: false,
                token => !requiresConfirmation || request.Confirmed
                    ? dispatcher.SendAsync(new ChangeOrganizationLifecycleForAdministrationCommand(
                        organizationId, action, request.ExpectedVersion, ResolveActor(actorContext)), token)
                    : Task.FromResult(Result.Failure<OrganizationDto>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static string ResolveActor(IAdminActorContext context) =>
        "admin:" + (context.Actor?.Id ?? "unknown");

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(OrganizationApplicationErrors.OrganizationNotFound.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.MembershipNotFound.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.OrganizationNotActive.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.OrganizationAlreadySuspended.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.OrganizationNotSuspended.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.OrganizationArchived.Code, StatusCodes.Status409Conflict));

    public sealed record ChangeOrganizationLifecycleAdminRequest(long ExpectedVersion, bool Confirmed);

    public sealed record EnsureOrganizationOwnerAdminRequest(
        string SubjectId,
        long ExpectedOrganizationVersion,
        long? ExpectedMembershipVersion,
        bool Confirmed);
}
