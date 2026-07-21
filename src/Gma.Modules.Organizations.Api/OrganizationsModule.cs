namespace Gma.Modules.Organizations.Api;

using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.ModuleComposition;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class OrganizationsModule : IModule
{
    public string Name => OrganizationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(OrganizationsProfiles.Default, "Gma.Modules.Organizations.Api");
        builder.Services.AddOptions<OrganizationsApiSecurityOptions>();
        builder.Services.AddOrganizationsApplication(builder.Configuration);
        builder.AddOrganizationsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        OrganizationsApiSecurityOptions security = endpoints.ServiceProvider
            .GetRequiredService<IOptions<OrganizationsApiSecurityOptions>>()
            .Value;
        OrganizationEndpoints.Map(endpoints, this.Name, security.GovernanceOperationsAssurance);
    }
}
