namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationProvisioningResult(
    OrganizationProvisioningOutcome Outcome,
    OrganizationMembershipSummaryDto? Summary,
    string? ErrorCode)
{
    public bool IsSuccess =>
        (this.Outcome is OrganizationProvisioningOutcome.Provisioned or
            OrganizationProvisioningOutcome.AlreadyProvisioned) &&
        this.Summary is not null &&
        this.ErrorCode is null;
}
