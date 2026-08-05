namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationJoinSourceAuthorizationOperationNames
{
    public static string ToWireName(
        OrganizationJoinSourceAuthorizationOperation operation) =>
        operation switch
        {
            OrganizationJoinSourceAuthorizationOperation.ReadInvitations =>
                "read-invitations",
            OrganizationJoinSourceAuthorizationOperation.IssueInvitation =>
                "issue-invitation",
            OrganizationJoinSourceAuthorizationOperation.RevokeInvitation =>
                "revoke-invitation",
            OrganizationJoinSourceAuthorizationOperation.ReissueInvitation =>
                "reissue-invitation",
            OrganizationJoinSourceAuthorizationOperation.ReadEnrollmentLinks =>
                "read-enrollment-links",
            OrganizationJoinSourceAuthorizationOperation.IssueEnrollmentLink =>
                "issue-enrollment-link",
            OrganizationJoinSourceAuthorizationOperation.DisableEnrollmentLink =>
                "disable-enrollment-link",
            OrganizationJoinSourceAuthorizationOperation.RotateEnrollmentLink =>
                "rotate-enrollment-link",
            OrganizationJoinSourceAuthorizationOperation.ReadJoinRequests =>
                "read-join-requests",
            OrganizationJoinSourceAuthorizationOperation.ResolveJoinRequest =>
                "resolve-join-request",
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unknown join-source authorization operations cannot be serialized.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationJoinSourceAuthorizationOperation operation)
    {
        operation = value switch
        {
            "read-invitations" =>
                OrganizationJoinSourceAuthorizationOperation.ReadInvitations,
            "issue-invitation" =>
                OrganizationJoinSourceAuthorizationOperation.IssueInvitation,
            "revoke-invitation" =>
                OrganizationJoinSourceAuthorizationOperation.RevokeInvitation,
            "reissue-invitation" =>
                OrganizationJoinSourceAuthorizationOperation.ReissueInvitation,
            "read-enrollment-links" =>
                OrganizationJoinSourceAuthorizationOperation.ReadEnrollmentLinks,
            "issue-enrollment-link" =>
                OrganizationJoinSourceAuthorizationOperation.IssueEnrollmentLink,
            "disable-enrollment-link" =>
                OrganizationJoinSourceAuthorizationOperation.DisableEnrollmentLink,
            "rotate-enrollment-link" =>
                OrganizationJoinSourceAuthorizationOperation.RotateEnrollmentLink,
            "read-join-requests" =>
                OrganizationJoinSourceAuthorizationOperation.ReadJoinRequests,
            "resolve-join-request" =>
                OrganizationJoinSourceAuthorizationOperation.ResolveJoinRequest,
            _ => OrganizationJoinSourceAuthorizationOperation.Unknown
        };
        return operation != OrganizationJoinSourceAuthorizationOperation.Unknown;
    }
}

internal sealed class OrganizationJoinSourceAuthorizationOperationJsonConverter
    : JsonConverter<OrganizationJoinSourceAuthorizationOperation>
{
    private const string EnumDisplayName =
        "OrganizationJoinSourceAuthorizationOperation";

    public override OrganizationJoinSourceAuthorizationOperation Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<
            OrganizationJoinSourceAuthorizationOperation>(
            ref reader,
            EnumDisplayName,
            OrganizationJoinSourceAuthorizationOperationNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationJoinSourceAuthorizationOperation value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            EnumDisplayName,
            OrganizationJoinSourceAuthorizationOperationNames.ToWireName);
}
