namespace Gma.Modules.Organizations.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;

internal sealed class OrganizationJoinTokenInspector(IRequestDispatcher dispatcher)
    : IOrganizationJoinTokenInspector
{
    public async Task<OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto>> InspectInvitationAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        Result<OrganizationInvitationPreviewDto> result = await dispatcher.QueryAsync(
            new PreviewOrganizationInvitationQuery(token),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? new OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto>(result.Value, null)
            : new OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto>(null, result.Error.Code);
    }

    public async Task<OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto>> InspectEnrollmentAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        Result<OrganizationEnrollmentPreviewDto> result = await dispatcher.QueryAsync(
            new PreviewOrganizationEnrollmentLinkQuery(token),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? new OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto>(result.Value, null)
            : new OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto>(null, result.Error.Code);
    }
}
