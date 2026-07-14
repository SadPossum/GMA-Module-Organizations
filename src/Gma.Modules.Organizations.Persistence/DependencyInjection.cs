namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
public static class DependencyInjection
{
    public static IHostApplicationBuilder AddOrganizationsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);

        builder.Services.TryAddModuleDbContext<OrganizationsDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                OrganizationsMigrations.SqlServerAssembly,
                OrganizationsMigrations.PostgreSqlAssembly,
                OrganizationsMigrations.Schema,
                OrganizationsMigrations.HistoryTable));

        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, OrganizationsUnitOfWork>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, OrganizationsOutboxWriter>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, OrganizationsOutboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, OrganizationsInboxStore>());
        builder.Services.TryAddScoped<IOrganizationRepository, OrganizationRepository>();

        return builder;
    }
}
