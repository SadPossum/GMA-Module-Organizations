namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationMembershipChangePolicy
{
    ValueTask<OrganizationMembershipChangePolicyDecision> EvaluateAsync(
        OrganizationMembershipChangePolicyRequest request,
        CancellationToken cancellationToken = default);
}
