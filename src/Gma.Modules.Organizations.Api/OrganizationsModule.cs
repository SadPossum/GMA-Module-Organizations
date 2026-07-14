namespace Gma.Modules.Organizations.Api;

using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
public sealed class OrganizationsModule : IModule
{
    public string Name => OrganizationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddOrganizationsApplication(builder.Configuration);
        builder.AddOrganizationsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        OrganizationEndpoints.Map(endpoints, this.Name);
    }
}
