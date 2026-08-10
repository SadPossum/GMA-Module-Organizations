namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationScopeExportStoreNames
{
    public static string ToWireName(OrganizationScopeExportStore store) =>
        store switch
        {
            OrganizationScopeExportStore.Organization => "organization",
            OrganizationScopeExportStore.Memberships => "memberships",
            OrganizationScopeExportStore.Invitations => "invitations",
            OrganizationScopeExportStore.EnrollmentLinks => "enrollment-links",
            OrganizationScopeExportStore.EnrollmentClaims => "enrollment-claims",
            _ => throw new ArgumentOutOfRangeException(
                nameof(store),
                store,
                "Organization scope export store is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationScopeExportStore store)
    {
        store = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "organization" => OrganizationScopeExportStore.Organization,
            "memberships" => OrganizationScopeExportStore.Memberships,
            "invitations" => OrganizationScopeExportStore.Invitations,
            "enrollment-links" => OrganizationScopeExportStore.EnrollmentLinks,
            "enrollment-claims" => OrganizationScopeExportStore.EnrollmentClaims,
            _ => OrganizationScopeExportStore.Unknown
        };
        return store is not OrganizationScopeExportStore.Unknown;
    }
}

internal sealed class OrganizationScopeExportStoreJsonConverter
    : JsonConverter<OrganizationScopeExportStore>
{
    public override OrganizationScopeExportStore Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationScopeExportStore>(
            ref reader,
            "Organization scope export store",
            OrganizationScopeExportStoreNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationScopeExportStore value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization scope export store",
            OrganizationScopeExportStoreNames.ToWireName);
}
