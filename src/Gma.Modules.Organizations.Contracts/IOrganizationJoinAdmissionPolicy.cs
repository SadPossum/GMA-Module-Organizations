namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationJoinAdmissionPolicy
{
    ValueTask<OrganizationJoinAdmissionDecision> EvaluateAsync(
        OrganizationJoinAdmissionContext context,
        CancellationToken cancellationToken = default);
}
