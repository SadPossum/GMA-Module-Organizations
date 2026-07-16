namespace Gma.Modules.Organizations.Application.Access;

using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;

internal sealed class OrganizationAccessDecisionReader(IOrganizationRepository organizations)
    : IOrganizationAccessDecisionReader
{
    public async Task<OrganizationAccessDecision> ReadAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        Organization? organization = await organizations
            .GetOrganizationAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is null)
        {
            return OrganizationAccessDecision.OrganizationNotFound;
        }

        if (organization.Status != OrganizationState.Active)
        {
            return OrganizationAccessDecision.OrganizationInactive;
        }

        OrganizationMembership? membership = await organizations
            .GetMembershipAsync(organizationId, subjectId, cancellationToken)
            .ConfigureAwait(false);
        if (membership is null)
        {
            return OrganizationAccessDecision.MembershipNotFound;
        }

        return membership.Status == OrganizationMembershipState.Active
            ? OrganizationAccessDecision.Allowed
            : OrganizationAccessDecision.MembershipInactive;
    }
}
