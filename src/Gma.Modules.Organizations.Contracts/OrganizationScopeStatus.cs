namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationScopeStatusJsonConverter))]
public enum OrganizationScopeStatus
{
    Unknown = 0,
    Invalid = 1,
    Missing = 2,
    Open = 3,
    Closed = 4
}
