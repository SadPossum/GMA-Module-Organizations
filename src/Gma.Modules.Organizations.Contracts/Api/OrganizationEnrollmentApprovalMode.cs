namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationEnrollmentApprovalModeJsonConverter))]
public enum OrganizationEnrollmentApprovalMode
{
    Unknown = 0,
    Automatic = 1,
    RequiresApproval = 2
}
