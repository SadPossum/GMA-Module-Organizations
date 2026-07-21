namespace Gma.Modules.Organizations.Contracts;

public enum OrganizationMembershipLifecycleOutcome
{
    Unknown = 0,
    Changed = 1,
    AlreadyInDesiredState = 2,
    NotFound = 3,
    OwnerProtected = 4,
    TransitionNotAllowed = 5
}
