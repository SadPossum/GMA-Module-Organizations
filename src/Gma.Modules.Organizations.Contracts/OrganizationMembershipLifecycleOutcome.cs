namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationMembershipLifecycleOutcomeJsonConverter))]
public enum OrganizationMembershipLifecycleOutcome
{
    Unknown = 0,
    Changed = 1,
    AlreadyInDesiredState = 2,
    NotFound = 3,
    OwnerProtected = 4,
    TransitionNotAllowed = 5
}
