namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationJoinAdmissionPolicy
{
    ValueTask<bool> IsAllowedAsync(
        OrganizationJoinAdmissionContext context,
        CancellationToken cancellationToken = default);
}
