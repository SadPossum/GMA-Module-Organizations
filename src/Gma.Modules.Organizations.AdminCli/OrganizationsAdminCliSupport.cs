namespace Gma.Modules.Organizations.AdminCli;

using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

internal static class OrganizationsAdminCliSupport
{
    public static Option<Guid> OrganizationIdOption() => new("--organization-id")
    {
        Description = "Organization id.",
        Required = true
    };

    public static Option<long> ExpectedVersionOption(string name = "--expected-version") => new(name)
    {
        Description = "Expected aggregate version.",
        Required = true
    };

    public static Option<Guid> OperationIdOption() => new("--operation-id")
    {
        Description = "Stable operation id retained for unchanged retries.",
        Required = true
    };

    public static string Actor(IServiceProvider provider) =>
        "admin:" + (provider.GetRequiredService<IAdminActorContext>().Actor?.Id ?? "unknown");

    public static string Output(ParseResult parseResult, AdminCliGlobalOptions options) =>
        parseResult.GetValue(options.OutputOption) ?? AdminCliOutput.Table;

    public static void WriteOrganizations(
        OrganizationCatalogListResponse response,
        string output)
    {
        if (AdminCliOutput.NormalizeFormat(output) == AdminCliOutput.Json)
        {
            AdminCliOutput.WriteObject(response, output);
            return;
        }

        AdminCliOutput.WriteRows(
            response.Items,
            output,
            [
                ("OrganizationId", item => item.OrganizationId.ToString()),
                ("Name", item => item.Name),
                ("Slug", item => item.Slug),
                ("Status", item => item.Status.ToString()),
                ("Owners", item => item.ActiveOwnerCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("Version", item => item.Version.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ]);
    }

    public static void WriteMembers(
        OrganizationMemberListResponse response,
        string output)
    {
        if (AdminCliOutput.NormalizeFormat(output) == AdminCliOutput.Json)
        {
            AdminCliOutput.WriteObject(response, output);
            return;
        }

        AdminCliOutput.WriteRows(
            response.Items,
            output,
            [
                ("MembershipId", item => item.MembershipId.ToString()),
                ("SubjectId", item => item.SubjectId),
                ("Role", item => item.Role.ToString()),
                ("Status", item => item.Status.ToString()),
                ("Version", item => item.Version.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ]);
    }
}
