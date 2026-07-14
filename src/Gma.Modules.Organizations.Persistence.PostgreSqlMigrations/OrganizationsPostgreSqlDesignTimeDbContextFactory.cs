namespace Gma.Modules.Organizations.Persistence.PostgreSqlMigrations;

using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class OrganizationsPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrganizationsDbContext>
{
    public OrganizationsDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<OrganizationsDbContext>(
                args,
                OrganizationsMigrations.PostgreSqlAssembly,
                OrganizationsMigrations.Schema,
                OrganizationsMigrations.HistoryTable));
}
