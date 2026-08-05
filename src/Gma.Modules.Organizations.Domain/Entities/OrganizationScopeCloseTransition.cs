namespace Gma.Modules.Organizations.Domain.Entities;

public enum OrganizationScopeCloseTransition
{
    Unknown = 0,
    Invalid = 1,
    Completed = 2,
    Replayed = 3,
    Conflict = 4
}
