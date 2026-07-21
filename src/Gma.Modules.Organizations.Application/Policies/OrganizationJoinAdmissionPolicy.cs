namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Modules.Organizations.Contracts;

internal sealed class OrganizationJoinAdmissionPolicy(
    IEnumerable<IOrganizationJoinAdmissionPolicy> policies)
{
    public async ValueTask<bool> IsAllowedAsync(
        OrganizationJoinAdmissionContext context,
        CancellationToken cancellationToken)
    {
        foreach (IOrganizationJoinAdmissionPolicy policy in policies)
        {
            if (!await policy.IsAllowedAsync(context, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }
}
