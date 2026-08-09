namespace Gma.Modules.Organizations.Application.Ports;

public interface IOrganizationGovernanceCoordinator
{
    Task AcquireSharedAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task AcquireExclusiveAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
}
