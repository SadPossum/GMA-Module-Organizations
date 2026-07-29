namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationEnrollmentClaimChangeJsonConverter))]
public enum OrganizationEnrollmentClaimChange
{
    Unknown = 0,
    Requested = 1,
    Accepted = 2,
    Rejected = 3
}
