namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationProvisioner
{
    Task<OrganizationProvisioningResult> ProvisionAsync(
        OrganizationProvisioningRequest request,
        CancellationToken cancellationToken = default);
}
