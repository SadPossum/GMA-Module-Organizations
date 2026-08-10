namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationAccessCandidateFilter
{
    Task<IReadOnlyList<string>> FilterAllowedAsync(
        Guid organizationId,
        IReadOnlyCollection<string> candidateSubjectIds,
        CancellationToken cancellationToken);
}
