namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationCreationAdmissionPolicy
{
    ValueTask<OrganizationCreationAdmissionDecision> EvaluateAsync(
        OrganizationCreationAdmissionRequest request,
        CancellationToken cancellationToken = default);
}
