namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationScopeDestructionStageNames
{
    public static string ToWireName(OrganizationScopeDestructionStage stage) =>
        stage switch
        {
            OrganizationScopeDestructionStage.InboxMessages => "inbox-messages",
            OrganizationScopeDestructionStage.OutboxMessages => "outbox-messages",
            OrganizationScopeDestructionStage.EnrollmentClaims => "enrollment-claims",
            OrganizationScopeDestructionStage.Invitations => "invitations",
            OrganizationScopeDestructionStage.EnrollmentLinks => "enrollment-links",
            OrganizationScopeDestructionStage.Memberships => "memberships",
            OrganizationScopeDestructionStage.Organization => "organization",
            OrganizationScopeDestructionStage.Completed => "completed",
            _ => throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                "Organization scope destruction stage is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationScopeDestructionStage stage)
    {
        stage = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "inbox-messages" => OrganizationScopeDestructionStage.InboxMessages,
            "outbox-messages" => OrganizationScopeDestructionStage.OutboxMessages,
            "enrollment-claims" => OrganizationScopeDestructionStage.EnrollmentClaims,
            "invitations" => OrganizationScopeDestructionStage.Invitations,
            "enrollment-links" => OrganizationScopeDestructionStage.EnrollmentLinks,
            "memberships" => OrganizationScopeDestructionStage.Memberships,
            "organization" => OrganizationScopeDestructionStage.Organization,
            "completed" => OrganizationScopeDestructionStage.Completed,
            _ => OrganizationScopeDestructionStage.Unknown
        };
        return stage is not OrganizationScopeDestructionStage.Unknown;
    }
}

internal sealed class OrganizationScopeDestructionStageJsonConverter
    : JsonConverter<OrganizationScopeDestructionStage>
{
    public override OrganizationScopeDestructionStage Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationScopeDestructionStage>(
            ref reader,
            "Organization scope destruction stage",
            OrganizationScopeDestructionStageNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationScopeDestructionStage value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization scope destruction stage",
            OrganizationScopeDestructionStageNames.ToWireName);
}
