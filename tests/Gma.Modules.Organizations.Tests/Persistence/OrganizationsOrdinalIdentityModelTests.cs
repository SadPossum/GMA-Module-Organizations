namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsOrdinalIdentityModelTests
{
    private const string SqlServerOrdinalCollation =
        "Latin1_General_100_BIN2";

    private static readonly (Type EntityType, string PropertyName)[]
        OpaqueIdentityProperties =
        [
            (typeof(Organization), nameof(Organization.CreatedBy)),
            (typeof(Organization), nameof(Organization.LastChangedBy)),
            (typeof(OrganizationMembership),
                nameof(OrganizationMembership.SubjectId)),
            (typeof(OrganizationMembership),
                nameof(OrganizationMembership.CreatedBy)),
            (typeof(OrganizationMembership),
                nameof(OrganizationMembership.LastChangedBy)),
            (typeof(OrganizationInvitation),
                nameof(OrganizationInvitation.InviterSubjectId)),
            (typeof(OrganizationInvitation),
                nameof(OrganizationInvitation.AcceptedSubjectId)),
            (typeof(OrganizationInvitation),
                nameof(OrganizationInvitation.CreatedBy)),
            (typeof(OrganizationInvitation),
                nameof(OrganizationInvitation.LastChangedBy)),
            (typeof(OrganizationEnrollmentLink),
                nameof(OrganizationEnrollmentLink.CreatorSubjectId)),
            (typeof(OrganizationEnrollmentLink),
                nameof(OrganizationEnrollmentLink.CreatedBy)),
            (typeof(OrganizationEnrollmentLink),
                nameof(OrganizationEnrollmentLink.LastChangedBy)),
            (typeof(OrganizationEnrollmentClaim),
                nameof(OrganizationEnrollmentClaim.SubjectId)),
            (typeof(OrganizationEnrollmentClaim),
                nameof(OrganizationEnrollmentClaim.LastChangedBy))
        ];

    [Fact]
    public void Sql_server_model_uses_ordinal_storage_for_every_opaque_identity()
    {
        using OrganizationsDbContext dbContext = CreateSqlServerDbContext();
        IModel model = dbContext.GetService<IDesignTimeModel>().Model;

        Assert.All(OpaqueIdentityProperties, identity =>
        {
            IProperty property = model.FindEntityType(identity.EntityType)?
                .FindProperty(identity.PropertyName) ??
                throw new InvalidOperationException(
                    $"Missing identity property {identity.EntityType.Name}.{identity.PropertyName}.");

            Assert.Equal(SqlServerOrdinalCollation, property.GetCollation());
        });
    }

    [Fact]
    public void PostgreSql_model_keeps_provider_default_identity_collation()
    {
        using OrganizationsDbContext dbContext = CreatePostgreSqlDbContext();
        IModel model = dbContext.GetService<IDesignTimeModel>().Model;

        Assert.All(OpaqueIdentityProperties, identity =>
        {
            IProperty property = model.FindEntityType(identity.EntityType)?
                .FindProperty(identity.PropertyName) ??
                throw new InvalidOperationException(
                    $"Missing identity property {identity.EntityType.Name}.{identity.PropertyName}.");

            Assert.Null(property.GetCollation());
        });
    }

    [Fact]
    public void Indexed_subject_queries_do_not_inject_query_level_collation()
    {
        using OrganizationsDbContext dbContext = CreateSqlServerDbContext();

        string sql = dbContext.Memberships
            .Where(membership =>
                membership.OrganizationId == Guid.Empty &&
                membership.SubjectId == "Case-Subject")
            .ToQueryString();

        Assert.DoesNotContain(
            "COLLATE",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    private static OrganizationsDbContext CreateSqlServerDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseSqlServer(
                    "Server=(local);Database=gma_organizations_identity;" +
                    "Trusted_Connection=True;TrustServerCertificate=True")
                .Options;
        return new OrganizationsDbContext(options);
    }

    private static OrganizationsDbContext CreatePostgreSqlDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=gma_organizations_identity;" +
                    "Username=postgres;Password=postgres")
                .Options;
        return new OrganizationsDbContext(options);
    }
}
