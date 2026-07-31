namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationMutationAdmissionDecisionJsonConverter))]
public enum OrganizationMutationAdmissionDecision
{
    Unknown = 0,
    Allowed = 1,
    Denied = 2,
    Unavailable = 3
}
