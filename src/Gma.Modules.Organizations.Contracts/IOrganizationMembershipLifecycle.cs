namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationMembershipLifecycle
{
    Task<OrganizationMembershipLifecycleResult> EnsureStateAsync(
        Guid organizationId,
        string subjectId,
        OrganizationMembershipStatus desiredStatus,
        string actorId,
        CancellationToken cancellationToken = default);
}
