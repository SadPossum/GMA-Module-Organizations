namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationChangeNames
{
    public static string ToWireName(OrganizationChange change) =>
        change switch
        {
            OrganizationChange.Created => "created",
            OrganizationChange.ProfileUpdated => "profile-updated",
            OrganizationChange.Suspended => "suspended",
            OrganizationChange.Reactivated => "reactivated",
            OrganizationChange.Archived => "archived",
            OrganizationChange.OwnerCountChanged => "owner-count-changed",
            OrganizationChange.OwnershipTransferred => "ownership-transferred",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, "Organization change is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationChange change)
    {
        change = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "created" => OrganizationChange.Created,
            "profile-updated" => OrganizationChange.ProfileUpdated,
            "suspended" => OrganizationChange.Suspended,
            "reactivated" => OrganizationChange.Reactivated,
            "archived" => OrganizationChange.Archived,
            "owner-count-changed" => OrganizationChange.OwnerCountChanged,
            "ownership-transferred" => OrganizationChange.OwnershipTransferred,
            _ => OrganizationChange.Unknown
        };
        return change is not OrganizationChange.Unknown;
    }
}

internal sealed class OrganizationChangeJsonConverter : JsonConverter<OrganizationChange>
{
    public override OrganizationChange Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationChange>(
            ref reader,
            "Organization change",
            OrganizationChangeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationChange value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization change",
            OrganizationChangeNames.ToWireName);
}
