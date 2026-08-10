namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationScopeExportStoreJsonConverter))]
public enum OrganizationScopeExportStore
{
    Unknown = 0,
    Organization = 1,
    Memberships = 2,
    Invitations = 3,
    EnrollmentLinks = 4,
    EnrollmentClaims = 5
}
