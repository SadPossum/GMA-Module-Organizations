namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationMembershipLifecycleOutcomeNames
{
    public static string ToWireName(OrganizationMembershipLifecycleOutcome outcome) =>
        outcome switch
        {
            OrganizationMembershipLifecycleOutcome.Changed => "changed",
            OrganizationMembershipLifecycleOutcome.AlreadyInDesiredState => "already-in-desired-state",
            OrganizationMembershipLifecycleOutcome.NotFound => "not-found",
            OrganizationMembershipLifecycleOutcome.OwnerProtected => "owner-protected",
            OrganizationMembershipLifecycleOutcome.TransitionNotAllowed => "transition-not-allowed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Organization membership lifecycle outcome is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationMembershipLifecycleOutcome outcome)
    {
        outcome = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "changed" => OrganizationMembershipLifecycleOutcome.Changed,
            "already-in-desired-state" => OrganizationMembershipLifecycleOutcome.AlreadyInDesiredState,
            "not-found" => OrganizationMembershipLifecycleOutcome.NotFound,
            "owner-protected" => OrganizationMembershipLifecycleOutcome.OwnerProtected,
            "transition-not-allowed" => OrganizationMembershipLifecycleOutcome.TransitionNotAllowed,
            _ => OrganizationMembershipLifecycleOutcome.Unknown
        };
        return outcome is not OrganizationMembershipLifecycleOutcome.Unknown;
    }
}

internal sealed class OrganizationMembershipLifecycleOutcomeJsonConverter
    : JsonConverter<OrganizationMembershipLifecycleOutcome>
{
    public override OrganizationMembershipLifecycleOutcome Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationMembershipLifecycleOutcome>(
            ref reader,
            "Organization membership lifecycle outcome",
            OrganizationMembershipLifecycleOutcomeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationMembershipLifecycleOutcome value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization membership lifecycle outcome",
            OrganizationMembershipLifecycleOutcomeNames.ToWireName);
}
