namespace Gma.Modules.Organizations.Domain.ValueObjects;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

public sealed record OrganizationName
{
    public const int MaxLength = 160;

    private OrganizationName(string value) => this.Value = value;

    public string Value { get; }

    public static Result<OrganizationName> Create(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is < 2 or > MaxLength || normalized.Any(char.IsControl)
            ? Result.Failure<OrganizationName>(OrganizationDomainErrors.NameInvalid)
            : Result.Success(new OrganizationName(normalized));
    }
}
