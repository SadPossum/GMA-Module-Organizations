namespace Gma.Modules.Organizations.Domain.Entities;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

public sealed class OrganizationScopeState
{
    public const int ScopeIdLength = 36;

    private OrganizationScopeState() { }

    private OrganizationScopeState(Guid organizationId)
    {
        this.OrganizationId = organizationId;
        this.ScopeId = organizationId.ToString("D");
    }

    public Guid OrganizationId { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public long Version { get; private set; }
    public bool IsClosed { get; private set; }
    public Guid? CloseOperationId { get; private set; }
    public string? CloseRequestSha256 { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public static Result<OrganizationScopeState> Create(Guid organizationId) =>
        organizationId != Guid.Empty
            ? Result.Success(new OrganizationScopeState(organizationId))
            : Result.Failure<OrganizationScopeState>(
                OrganizationDomainErrors.ScopeStateInvalid);

    public bool RegisterMutation()
    {
        if (this.IsClosed || this.Version == long.MaxValue)
        {
            return false;
        }

        this.Version++;
        return true;
    }

    public OrganizationScopeCloseTransition Close(
        Guid operationId,
        string requestSha256,
        DateTimeOffset closedAtUtc)
    {
        if (operationId == Guid.Empty ||
            !IsSha256(requestSha256) ||
            closedAtUtc == default)
        {
            return OrganizationScopeCloseTransition.Invalid;
        }

        if (this.IsClosed)
        {
            return this.CloseOperationId == operationId &&
                string.Equals(
                    this.CloseRequestSha256,
                    requestSha256,
                    StringComparison.Ordinal)
                ? OrganizationScopeCloseTransition.Replayed
                : OrganizationScopeCloseTransition.Conflict;
        }

        if (this.Version == long.MaxValue)
        {
            return OrganizationScopeCloseTransition.Invalid;
        }

        this.Version++;
        this.IsClosed = true;
        this.CloseOperationId = operationId;
        this.CloseRequestSha256 = requestSha256;
        this.ClosedAtUtc = closedAtUtc;
        return OrganizationScopeCloseTransition.Completed;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
