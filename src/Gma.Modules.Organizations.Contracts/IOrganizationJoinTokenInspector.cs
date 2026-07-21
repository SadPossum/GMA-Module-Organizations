namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationJoinTokenInspector
{
    Task<OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto>> InspectInvitationAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto>> InspectEnrollmentAsync(
        string token,
        CancellationToken cancellationToken = default);
}

public sealed record OrganizationJoinTokenInspection<TPreview>(
    TPreview? Preview,
    string? ErrorCode)
    where TPreview : class
{
    public bool IsSuccess => this.Preview is not null;
}
