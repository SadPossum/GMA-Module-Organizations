namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationJoinAdmissionOperationNames
{
    public static string ToWireName(OrganizationJoinAdmissionOperation operation) =>
        operation switch
        {
            OrganizationJoinAdmissionOperation.AcceptInvitation => "accept-invitation",
            OrganizationJoinAdmissionOperation.ClaimEnrollment => "claim-enrollment",
            OrganizationJoinAdmissionOperation.ApproveEnrollment => "approve-enrollment",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Organization join admission operation is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationJoinAdmissionOperation operation)
    {
        operation = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "accept-invitation" => OrganizationJoinAdmissionOperation.AcceptInvitation,
            "claim-enrollment" => OrganizationJoinAdmissionOperation.ClaimEnrollment,
            "approve-enrollment" => OrganizationJoinAdmissionOperation.ApproveEnrollment,
            _ => OrganizationJoinAdmissionOperation.Unknown
        };
        return operation is not OrganizationJoinAdmissionOperation.Unknown;
    }
}

internal sealed class OrganizationJoinAdmissionOperationJsonConverter
    : JsonConverter<OrganizationJoinAdmissionOperation>
{
    public override OrganizationJoinAdmissionOperation Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationJoinAdmissionOperation>(
            ref reader,
            "Organization join admission operation",
            OrganizationJoinAdmissionOperationNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationJoinAdmissionOperation value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization join admission operation",
            OrganizationJoinAdmissionOperationNames.ToWireName);
}
