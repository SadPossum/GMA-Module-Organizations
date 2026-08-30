namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationProvisioningOutcomeJsonConverter))]
public enum OrganizationProvisioningOutcome
{
    Unknown = 0,
    Provisioned = 1,
    AlreadyProvisioned = 2,
    InvalidRequest = 3,
    IdentityConflict = 4,
    SlugConflict = 5
}
