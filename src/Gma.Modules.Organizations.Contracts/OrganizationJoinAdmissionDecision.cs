namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationJoinAdmissionDecisionJsonConverter))]
public enum OrganizationJoinAdmissionDecision
{
    Unknown = 0,
    Allowed = 1,
    Denied = 2,
    Unavailable = 3
}
