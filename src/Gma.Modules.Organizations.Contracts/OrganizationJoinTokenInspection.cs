namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationJoinTokenInspection<TPreview>(
    TPreview? Preview,
    string? ErrorCode)
    where TPreview : class
{
    public bool IsSuccess => this.Preview is not null;
}
