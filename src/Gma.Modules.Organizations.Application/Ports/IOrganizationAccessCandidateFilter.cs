namespace Gma.Modules.Organizations.Application.Ports;

public interface IOrganizationAccessCandidateFilter
{
    const int MaximumCandidateCount = 500;

    Task<IReadOnlyList<string>> FilterAllowedAsync(
        Guid organizationId,
        IReadOnlyCollection<string> candidateSubjectIds,
        CancellationToken cancellationToken);
}
