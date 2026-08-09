namespace Gma.Modules.Organizations.Application.Ports;

public interface IOrganizationJoinSubjectCoordinator
{
    Task AcquireAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken);
}
