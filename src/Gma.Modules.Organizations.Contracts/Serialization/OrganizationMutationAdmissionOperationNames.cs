namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationMutationAdmissionOperationNames
{
    public static string ToWireName(OrganizationMutationAdmissionOperation operation) =>
        operation switch
        {
            OrganizationMutationAdmissionOperation.UpdateOrganization => "update-organization",
            OrganizationMutationAdmissionOperation.SuspendOrganization => "suspend-organization",
            OrganizationMutationAdmissionOperation.ReactivateOrganization => "reactivate-organization",
            OrganizationMutationAdmissionOperation.ArchiveOrganization => "archive-organization",
            OrganizationMutationAdmissionOperation.TransferOwnership => "transfer-ownership",
            OrganizationMutationAdmissionOperation.IssueInvitation => "issue-invitation",
            OrganizationMutationAdmissionOperation.ReissueInvitation => "reissue-invitation",
            OrganizationMutationAdmissionOperation.IssueEnrollmentLink => "issue-enrollment-link",
            OrganizationMutationAdmissionOperation.RotateEnrollmentLink => "rotate-enrollment-link",
            OrganizationMutationAdmissionOperation.RestoreMembership => "restore-membership",
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Organization mutation admission operation is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationMutationAdmissionOperation operation)
    {
        operation = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "update-organization" => OrganizationMutationAdmissionOperation.UpdateOrganization,
            "suspend-organization" => OrganizationMutationAdmissionOperation.SuspendOrganization,
            "reactivate-organization" => OrganizationMutationAdmissionOperation.ReactivateOrganization,
            "archive-organization" => OrganizationMutationAdmissionOperation.ArchiveOrganization,
            "transfer-ownership" => OrganizationMutationAdmissionOperation.TransferOwnership,
            "issue-invitation" => OrganizationMutationAdmissionOperation.IssueInvitation,
            "reissue-invitation" => OrganizationMutationAdmissionOperation.ReissueInvitation,
            "issue-enrollment-link" => OrganizationMutationAdmissionOperation.IssueEnrollmentLink,
            "rotate-enrollment-link" => OrganizationMutationAdmissionOperation.RotateEnrollmentLink,
            "restore-membership" => OrganizationMutationAdmissionOperation.RestoreMembership,
            _ => OrganizationMutationAdmissionOperation.Unknown
        };
        return operation is not OrganizationMutationAdmissionOperation.Unknown;
    }
}

internal sealed class OrganizationMutationAdmissionOperationJsonConverter
    : JsonConverter<OrganizationMutationAdmissionOperation>
{
    public override OrganizationMutationAdmissionOperation Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationMutationAdmissionOperation>(
            ref reader,
            "Organization mutation admission operation",
            OrganizationMutationAdmissionOperationNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationMutationAdmissionOperation value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization mutation admission operation",
            OrganizationMutationAdmissionOperationNames.ToWireName);
}
