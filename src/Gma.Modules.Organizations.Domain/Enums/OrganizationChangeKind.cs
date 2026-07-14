namespace Gma.Modules.Organizations.Domain.Enums;

public enum OrganizationChangeKind
{
    Unknown = 0,
    Created = 1,
    ProfileUpdated = 2,
    Suspended = 3,
    Reactivated = 4,
    Archived = 5,
    OwnerCountChanged = 6,
    OwnershipTransferred = 7
}
