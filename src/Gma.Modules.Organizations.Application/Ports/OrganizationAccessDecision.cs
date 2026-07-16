namespace Gma.Modules.Organizations.Application.Ports;

public enum OrganizationAccessDecision
{
    Unknown = 0,
    Allowed = 1,
    OrganizationNotFound = 2,
    OrganizationInactive = 3,
    MembershipNotFound = 4,
    MembershipInactive = 5
}
