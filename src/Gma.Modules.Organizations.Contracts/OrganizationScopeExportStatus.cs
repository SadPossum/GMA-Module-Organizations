namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationScopeExportStatusJsonConverter))]
public enum OrganizationScopeExportStatus
{
    Unknown = 0,
    Invalid = 1,
    Completed = 2,
    Missing = 3,
    Closed = 4,
    Stale = 5
}
