namespace Gma.Modules.Organizations.Domain.ValueObjects;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

public sealed record OrganizationSlug
{
    public const int MinLength = 3;
    public const int MaxLength = 64;

    private OrganizationSlug(string value) => this.Value = value;

    public string Value { get; }

    public static Result<OrganizationSlug> Create(string? value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        bool valid = normalized.Length is >= MinLength and <= MaxLength &&
                     normalized[0] != '-' &&
                     normalized[^1] != '-' &&
                     !normalized.Contains("--", StringComparison.Ordinal) &&
                     normalized.All(character =>
                         character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

        return valid
            ? Result.Success(new OrganizationSlug(normalized))
            : Result.Failure<OrganizationSlug>(OrganizationDomainErrors.SlugInvalid);
    }
}
