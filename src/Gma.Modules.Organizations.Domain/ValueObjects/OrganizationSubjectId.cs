namespace Gma.Modules.Organizations.Domain.ValueObjects;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

public sealed record OrganizationSubjectId
{
    public const int MaxLength = 160;

    private OrganizationSubjectId(string value) => this.Value = value;

    public string Value { get; }

    public static Result<OrganizationSubjectId> Create(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is < 1 or > MaxLength || normalized.Any(char.IsWhiteSpace) || normalized.Any(char.IsControl)
            ? Result.Failure<OrganizationSubjectId>(OrganizationDomainErrors.SubjectInvalid)
            : Result.Success(new OrganizationSubjectId(normalized));
    }
}
