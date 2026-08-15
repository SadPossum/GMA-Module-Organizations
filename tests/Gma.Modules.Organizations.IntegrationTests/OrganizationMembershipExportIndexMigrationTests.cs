namespace Gma.Modules.Organizations.IntegrationTests;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PostgreSqlMigration =
    Gma.Modules.Organizations.Persistence.PostgreSqlMigrations.Migrations
        .AddOrganizationMembershipExportIndex;
using SqlServerMigration =
    Gma.Modules.Organizations.Persistence.SqlServerMigrations.Migrations
        .AddOrganizationMembershipExportIndex;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationMembershipExportIndexMigrationTests
{
    private const string IndexName =
        "IX_organization_memberships_OrganizationId_Id";

    [Fact]
    public void PostgreSql_migration_adds_the_membership_export_keyset_index() =>
        AssertMigration(
            new PostgreSqlMigrationProbe().BuildUpOperations(),
            new PostgreSqlMigrationProbe().BuildDownOperations());

    [Fact]
    public void Sql_server_migration_adds_the_membership_export_keyset_index() =>
        AssertMigration(
            new SqlServerMigrationProbe().BuildUpOperations(),
            new SqlServerMigrationProbe().BuildDownOperations());

    private static void AssertMigration(
        IReadOnlyList<MigrationOperation> upOperations,
        IReadOnlyList<MigrationOperation> downOperations)
    {
        CreateIndexOperation create = Assert.IsType<CreateIndexOperation>(
            Assert.Single(upOperations));
        Assert.Equal(IndexName, create.Name);
        Assert.Equal("organizations", create.Schema);
        Assert.Equal("organization_memberships", create.Table);
        Assert.Equal(["OrganizationId", "Id"], create.Columns);
        Assert.False(create.IsUnique);

        DropIndexOperation drop = Assert.IsType<DropIndexOperation>(
            Assert.Single(downOperations));
        Assert.Equal(IndexName, drop.Name);
        Assert.Equal("organizations", drop.Schema);
        Assert.Equal("organization_memberships", drop.Table);
    }

    private sealed class PostgreSqlMigrationProbe : PostgreSqlMigration
    {
        public List<MigrationOperation> BuildUpOperations()
        {
            MigrationBuilder builder = new(
                "Npgsql.EntityFrameworkCore.PostgreSQL");
            base.Up(builder);
            return builder.Operations;
        }

        public List<MigrationOperation> BuildDownOperations()
        {
            MigrationBuilder builder = new(
                "Npgsql.EntityFrameworkCore.PostgreSQL");
            base.Down(builder);
            return builder.Operations;
        }
    }

    private sealed class SqlServerMigrationProbe : SqlServerMigration
    {
        public List<MigrationOperation> BuildUpOperations()
        {
            MigrationBuilder builder = new(
                "Microsoft.EntityFrameworkCore.SqlServer");
            base.Up(builder);
            return builder.Operations;
        }

        public List<MigrationOperation> BuildDownOperations()
        {
            MigrationBuilder builder = new(
                "Microsoft.EntityFrameworkCore.SqlServer");
            base.Down(builder);
            return builder.Operations;
        }
    }
}
