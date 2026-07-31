namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationMutationAdmissionPolicy
{
    ValueTask<OrganizationMutationAdmissionDecision> EvaluateAsync(
        OrganizationMutationAdmissionContext context,
        CancellationToken cancellationToken = default);
}
