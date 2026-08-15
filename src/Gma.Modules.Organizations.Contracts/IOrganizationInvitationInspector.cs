namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationInvitationInspector
{
    Task<OrganizationInvitationStatus?> FindStatusAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default);
}
