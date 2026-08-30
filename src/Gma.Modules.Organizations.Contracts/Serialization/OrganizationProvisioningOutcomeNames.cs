namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationProvisioningOutcomeNames
{
    public static string ToWireName(OrganizationProvisioningOutcome outcome) =>
        outcome switch
        {
            OrganizationProvisioningOutcome.Provisioned => "provisioned",
            OrganizationProvisioningOutcome.AlreadyProvisioned => "already-provisioned",
            OrganizationProvisioningOutcome.InvalidRequest => "invalid-request",
            OrganizationProvisioningOutcome.IdentityConflict => "identity-conflict",
            OrganizationProvisioningOutcome.SlugConflict => "slug-conflict",
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Organization provisioning outcome is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationProvisioningOutcome outcome)
    {
        outcome = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "provisioned" => OrganizationProvisioningOutcome.Provisioned,
            "already-provisioned" =>
                OrganizationProvisioningOutcome.AlreadyProvisioned,
            "invalid-request" => OrganizationProvisioningOutcome.InvalidRequest,
            "identity-conflict" =>
                OrganizationProvisioningOutcome.IdentityConflict,
            "slug-conflict" => OrganizationProvisioningOutcome.SlugConflict,
            _ => OrganizationProvisioningOutcome.Unknown
        };
        return outcome is not OrganizationProvisioningOutcome.Unknown;
    }
}

internal sealed class OrganizationProvisioningOutcomeJsonConverter
    : JsonConverter<OrganizationProvisioningOutcome>
{
    public override OrganizationProvisioningOutcome Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationProvisioningOutcome>(
            ref reader,
            "Organization provisioning outcome",
            OrganizationProvisioningOutcomeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationProvisioningOutcome value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization provisioning outcome",
            OrganizationProvisioningOutcomeNames.ToWireName);
}
