namespace Gma.Modules.Organizations.AdminCli;

using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Admin.Contracts;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

internal static class OrganizationCatalogCommands
{
    public static Command CreateList(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<int> page = PageOption();
        Option<int> pageSize = PageSizeOption();
        Command command = new("list", "List organizations.") { page, pageSize };
        command.SetAction((parseResult, cancellationToken) =>
            services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
                parseResult,
                AdminOperation.Create(OrganizationsAdminOperationNames.CatalogList, OrganizationsAdminPermissions.Read),
                tenantId: null,
                requireTenant: false,
                async (provider, token) =>
                {
                    Result<OrganizationCatalogListResponse> result = await provider
                        .GetRequiredService<IRequestDispatcher>()
                        .QueryAsync(new ListOrganizationCatalogForAdministrationQuery(
                            parseResult.GetValue(page), parseResult.GetValue(pageSize)), token)
                        .ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        OrganizationsAdminCliSupport.WriteOrganizations(
                            result.Value, OrganizationsAdminCliSupport.Output(parseResult, globalOptions));
                    }

                    return result;
                },
                cancellationToken));
        return command;
    }

    public static Command CreateMembers(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> organizationId = OrganizationsAdminCliSupport.OrganizationIdOption();
        Option<int> page = PageOption();
        Option<int> pageSize = PageSizeOption();
        Command command = new("members", "List organization memberships.")
        {
            organizationId,
            page,
            pageSize
        };
        command.SetAction((parseResult, cancellationToken) =>
            services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
                parseResult,
                AdminOperation.Create(OrganizationsAdminOperationNames.MembersList, OrganizationsAdminPermissions.Read),
                tenantId: null,
                requireTenant: false,
                async (provider, token) =>
                {
                    Result<OrganizationMemberListResponse> result = await provider
                        .GetRequiredService<IRequestDispatcher>()
                        .QueryAsync(new ListOrganizationMembersForAdministrationQuery(
                            parseResult.GetRequiredValue(organizationId),
                            parseResult.GetValue(page), parseResult.GetValue(pageSize)), token)
                        .ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        OrganizationsAdminCliSupport.WriteMembers(
                            result.Value, OrganizationsAdminCliSupport.Output(parseResult, globalOptions));
                    }

                    return result;
                },
                cancellationToken));
        return command;
    }

    private static Option<int> PageOption() => new("--page")
    {
        Description = "Page number.",
        DefaultValueFactory = _ => PageRequest.DefaultPage
    };

    private static Option<int> PageSizeOption() => new("--page-size")
    {
        Description = "Page size.",
        DefaultValueFactory = _ => PageRequest.DefaultPageSize
    };
}
