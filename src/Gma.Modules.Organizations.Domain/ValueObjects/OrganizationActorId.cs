namespace Gma.Modules.Organizations.Domain.ValueObjects;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

public sealed record OrganizationActorId
{
    public const int MaxLength = 192;

    private OrganizationActorId(string value) => this.Value = value;

    public string Value { get; }

    public static Result<OrganizationActorId> Create(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is < 1 or > MaxLength || normalized.Any(char.IsControl)
            ? Result.Failure<OrganizationActorId>(OrganizationDomainErrors.ActorInvalid)
            : Result.Success(new OrganizationActorId(normalized));
    }
}
