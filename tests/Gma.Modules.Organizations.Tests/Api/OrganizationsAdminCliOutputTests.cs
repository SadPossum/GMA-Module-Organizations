namespace Gma.Modules.Organizations.Tests;

using System.Text.Json;
using Gma.Framework.Administration.Cli;
using Gma.Modules.Organizations.AdminCli;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
[Collection(ConsoleTestIsolation.Name)]
public sealed class OrganizationsAdminCliOutputTests
{
    [Fact]
    public void Organization_catalog_json_preserves_pagination_envelope()
    {
        var organization = new OrganizationDto(
            Guid.NewGuid(),
            "tenant-one",
            "One",
            "one",
            OrganizationStatus.Active,
            1,
            3,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        string json = Capture(() => OrganizationsAdminCliSupport.WriteOrganizations(
            new OrganizationCatalogListResponse([organization], 2, 25, true),
            AdminCliOutput.Json));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(25, root.GetProperty("pageSize").GetInt32());
        Assert.True(root.GetProperty("hasMore").GetBoolean());
        Assert.Equal(organization.OrganizationId, root.GetProperty("items")[0]
            .GetProperty("organizationId").GetGuid());
    }

    [Fact]
    public void Organization_members_json_preserves_pagination_envelope()
    {
        var membership = new OrganizationMembershipDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "subject-one",
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Active,
            4,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        string json = Capture(() => OrganizationsAdminCliSupport.WriteMembers(
            new OrganizationMemberListResponse([membership], 3, 10, true),
            AdminCliOutput.Json));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(3, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.True(root.GetProperty("hasMore").GetBoolean());
        Assert.Equal(membership.MembershipId, root.GetProperty("items")[0]
            .GetProperty("membershipId").GetGuid());
    }

    private static string Capture(Action write)
    {
        using StringWriter output = new();
        TextWriter originalOutput = Console.Out;
        Console.SetOut(output);
        try
        {
            write();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        return output.ToString();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleTestIsolation
{
    public const string Name = "Console";
}
