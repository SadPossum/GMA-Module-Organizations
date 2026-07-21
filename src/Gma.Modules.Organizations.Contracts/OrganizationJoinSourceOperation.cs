namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationJoinSourceOperation<TValue>(
    TValue? Value,
    string? ErrorCode)
    where TValue : class
{
    public bool IsSuccess => this.Value is not null && this.ErrorCode is null;
}
