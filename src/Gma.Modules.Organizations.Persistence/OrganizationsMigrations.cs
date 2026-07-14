namespace Gma.Modules.Organizations.Persistence;

using Gma.Modules.Organizations.Contracts;

public static class OrganizationsMigrations
{
    public const string Schema = OrganizationsModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Gma.Modules.Organizations.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Gma.Modules.Organizations.Persistence.PostgreSqlMigrations";
}
