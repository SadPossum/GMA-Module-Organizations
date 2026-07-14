namespace Gma.Modules.Organizations.Persistence.SqlServerMigrations;

using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class OrganizationsSqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrganizationsDbContext>
{
    public OrganizationsDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreateSqlServerOptions<OrganizationsDbContext>(
                args,
                OrganizationsMigrations.SqlServerAssembly,
                OrganizationsMigrations.Schema,
                OrganizationsMigrations.HistoryTable));
}
