namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationEnrollmentClaimStatusJsonConverter))]
public enum OrganizationEnrollmentClaimStatus
{
    Unknown = 0,
    Pending = 1,
    Accepted = 2,
    Rejected = 3,
    Expired = 4
}
