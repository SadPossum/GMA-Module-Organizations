namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationCreationAdmissionDecisionJsonConverter))]
public enum OrganizationCreationAdmissionDecision
{
    Unknown = 0,
    Allowed = 1,
    Denied = 2,
    SubjectVerificationRequired = 3,
    Unavailable = 4
}
