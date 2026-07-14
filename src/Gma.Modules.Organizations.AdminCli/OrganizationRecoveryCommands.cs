namespace Gma.Modules.Organizations.AdminCli;

using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Admin.Contracts;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

internal static class OrganizationRecoveryCommands
{
    public static Command CreateLifecycle(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        string name,
        bool requiresConfirmation)
    {
        Option<Guid> organizationId = OrganizationsAdminCliSupport.OrganizationIdOption();
        Option<long> expectedVersion = OrganizationsAdminCliSupport.ExpectedVersionOption();
        Option<bool> yes = new("--yes") { Description = "Confirm the operation." };
        Command command = new(name, $"{char.ToUpperInvariant(name[0])}{name[1..]} an organization.")
        {
            organizationId,
            expectedVersion,
            yes
        };
        command.SetAction((parseResult, cancellationToken) =>
            services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
                parseResult,
                AdminOperation.Create(OrganizationsAdminOperationNames.LifecycleChange, OrganizationsAdminPermissions.Manage),
                tenantId: null,
                requireTenant: false,
                async (provider, token) =>
                {
                    Result<OrganizationDto> result = requiresConfirmation && !parseResult.GetValue(yes)
                        ? Result.Failure<OrganizationDto>(AdminErrors.ConfirmationRequired)
                        : await provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                            new ChangeOrganizationLifecycleForAdministrationCommand(
                                parseResult.GetRequiredValue(organizationId),
                                ParseLifecycle(name),
                                parseResult.GetRequiredValue(expectedVersion),
                                OrganizationsAdminCliSupport.Actor(provider)), token).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        OrganizationsAdminCliSupport.WriteOrganizations(
                            new OrganizationCatalogListResponse([result.Value], 1, 1),
                            OrganizationsAdminCliSupport.Output(parseResult, globalOptions));
                    }

                    return result;
                },
                cancellationToken));
        return command;
    }

    public static Command CreateEnsureOwner(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> organizationId = OrganizationsAdminCliSupport.OrganizationIdOption();
        Option<string> subjectId = new("--subject-id")
        {
            Description = "Subject id to ensure as owner.",
            Required = true
        };
        Option<long> organizationVersion = OrganizationsAdminCliSupport.ExpectedVersionOption(
            "--expected-organization-version");
        Option<long?> membershipVersion = new("--expected-membership-version")
        {
            Description = "Expected membership version; omit when the membership does not exist."
        };
        Option<bool> yes = new("--yes") { Description = "Confirm owner recovery." };
        Command command = new("ensure-owner", "Create, restore, or promote an organization owner.")
        {
            organizationId,
            subjectId,
            organizationVersion,
            membershipVersion,
            yes
        };
        command.SetAction((parseResult, cancellationToken) =>
            services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
                parseResult,
                AdminOperation.Create(OrganizationsAdminOperationNames.OwnerEnsure, OrganizationsAdminPermissions.Manage),
                tenantId: null,
                requireTenant: false,
                async (provider, token) =>
                {
                    Result<OrganizationMembershipSummaryDto> result = !parseResult.GetValue(yes)
                        ? Result.Failure<OrganizationMembershipSummaryDto>(AdminErrors.ConfirmationRequired)
                        : await provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                            new EnsureOrganizationOwnerForAdministrationCommand(
                                parseResult.GetRequiredValue(organizationId),
                                parseResult.GetRequiredValue(subjectId),
                                parseResult.GetRequiredValue(organizationVersion),
                                parseResult.GetValue(membershipVersion),
                                OrganizationsAdminCliSupport.Actor(provider)), token).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        OrganizationsAdminCliSupport.WriteMembers(
                            new OrganizationMemberListResponse([result.Value.Membership], 1, 1),
                            OrganizationsAdminCliSupport.Output(parseResult, globalOptions));
                    }

                    return result;
                },
                cancellationToken));
        return command;
    }

    private static OrganizationLifecycleAction ParseLifecycle(string name) => name switch
    {
        "suspend" => OrganizationLifecycleAction.Suspend,
        "reactivate" => OrganizationLifecycleAction.Reactivate,
        "archive" => OrganizationLifecycleAction.Archive,
        _ => OrganizationLifecycleAction.Unknown
    };
}
