namespace Gma.Modules.Organizations.Application.Ports;

using Gma.Modules.Organizations.Domain.Aggregates;

public interface IOrganizationCreationCoordinator
{
    Task<Organization?> AcquireAsync(
        Guid operationId,
        CancellationToken cancellationToken);
}
