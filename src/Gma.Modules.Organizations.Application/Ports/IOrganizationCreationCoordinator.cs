namespace Gma.Modules.Organizations.Application.Ports;

using Gma.Modules.Organizations.Domain.Aggregates;

public interface IOrganizationCreationCoordinator
{
    Task<OrganizationCreationAcquisition> AcquireAsync(
        Guid operationId,
        CancellationToken cancellationToken);
}

public sealed record OrganizationCreationAcquisition(
    Organization? Organization,
    bool IsScopeClosed);
