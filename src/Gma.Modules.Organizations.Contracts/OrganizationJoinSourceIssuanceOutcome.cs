namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationJoinSourceIssuanceOutcomeJsonConverter))]
public enum OrganizationJoinSourceIssuanceOutcome
{
    Unknown = 0,
    Issued = 1,
    AlreadyIssued = 2
}
