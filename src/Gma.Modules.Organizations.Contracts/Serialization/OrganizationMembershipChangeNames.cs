namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationMembershipChangeNames
{
    public static string ToWireName(OrganizationMembershipChange change) =>
        change switch
        {
            OrganizationMembershipChange.Joined => "joined",
            OrganizationMembershipChange.Suspended => "suspended",
            OrganizationMembershipChange.Resumed => "resumed",
            OrganizationMembershipChange.Removed => "removed",
            OrganizationMembershipChange.PromotedToOwner => "promoted-to-owner",
            OrganizationMembershipChange.DemotedToMember => "demoted-to-member",
            OrganizationMembershipChange.Restored => "restored",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, "Organization membership change is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationMembershipChange change)
    {
        change = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "joined" => OrganizationMembershipChange.Joined,
            "suspended" => OrganizationMembershipChange.Suspended,
            "resumed" => OrganizationMembershipChange.Resumed,
            "removed" => OrganizationMembershipChange.Removed,
            "promoted-to-owner" => OrganizationMembershipChange.PromotedToOwner,
            "demoted-to-member" => OrganizationMembershipChange.DemotedToMember,
            "restored" => OrganizationMembershipChange.Restored,
            _ => OrganizationMembershipChange.Unknown
        };
        return change is not OrganizationMembershipChange.Unknown;
    }
}

internal sealed class OrganizationMembershipChangeJsonConverter
    : JsonConverter<OrganizationMembershipChange>
{
    public override OrganizationMembershipChange Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationMembershipChange>(
            ref reader,
            "Organization membership change",
            OrganizationMembershipChangeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationMembershipChange value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization membership change",
            OrganizationMembershipChangeNames.ToWireName);
}
