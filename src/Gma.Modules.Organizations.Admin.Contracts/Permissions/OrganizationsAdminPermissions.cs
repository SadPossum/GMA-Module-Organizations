namespace Gma.Modules.Organizations.Admin.Contracts;

using Gma.Modules.Organizations.Contracts;
using Gma.Framework.Administration;

public static class OrganizationsAdminPermissions
{
    public static readonly AdminPermission Read = AdminPermission.Create(OrganizationsAdminPermissionCodes.Read);
    public static readonly AdminPermission Manage = AdminPermission.Create(OrganizationsAdminPermissionCodes.Manage);
}
