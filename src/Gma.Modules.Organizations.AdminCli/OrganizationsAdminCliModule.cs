namespace Gma.Modules.Organizations.AdminCli;

using Gma.Framework.Administration.Cli;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

public sealed class OrganizationsAdminCliModule : IAdminCliModule
{
    public string Name => OrganizationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddOrganizationsApplication(builder.Configuration);
        builder.AddOrganizationsPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command module = new(OrganizationsModuleMetadata.Name, "Organizations administration operations.")
        {
            OrganizationCatalogCommands.CreateList(commands.Services, globalOptions),
            OrganizationCatalogCommands.CreateMembers(commands.Services, globalOptions),
            OrganizationRecoveryCommands.CreateLifecycle(
                commands.Services, globalOptions, "suspend", requiresConfirmation: true),
            OrganizationRecoveryCommands.CreateLifecycle(
                commands.Services, globalOptions, "reactivate", requiresConfirmation: false),
            OrganizationRecoveryCommands.CreateLifecycle(
                commands.Services, globalOptions, "archive", requiresConfirmation: true),
            OrganizationRecoveryCommands.CreateEnsureOwner(commands.Services, globalOptions)
        };
        commands.AddCommand(this.Name, module);
    }
}
