namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationJoinSourceAuthorizationPolicy
{
    ValueTask<OrganizationJoinSourceAuthorizationDecision> EvaluateAsync(
        OrganizationJoinSourceAuthorizationContext context,
        CancellationToken cancellationToken = default);
}
