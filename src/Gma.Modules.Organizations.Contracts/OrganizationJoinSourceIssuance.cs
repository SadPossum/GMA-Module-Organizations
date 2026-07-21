namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationJoinSourceIssuance<TSource>(
    TSource? Source,
    OrganizationJoinSourceIssuanceOutcome Outcome,
    string? Token,
    string? ErrorCode)
    where TSource : class
{
    public bool IsSuccess => this.ErrorCode is null && this.Source is not null && this.Outcome switch
    {
        OrganizationJoinSourceIssuanceOutcome.Issued => !string.IsNullOrWhiteSpace(this.Token),
        OrganizationJoinSourceIssuanceOutcome.AlreadyIssued => this.Token is null,
        _ => false
    };

    public bool HasNewToken =>
        this.Outcome == OrganizationJoinSourceIssuanceOutcome.Issued &&
        !string.IsNullOrWhiteSpace(this.Token);
}
