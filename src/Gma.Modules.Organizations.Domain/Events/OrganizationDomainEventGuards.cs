namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.ValueObjects;

internal static class OrganizationDomainEventGuards
{
    public static Guid RequireId(Guid value, string parameterName) =>
        DomainEventGuards.RequireId(value, parameterName);

    public static Guid? RequireOptionalId(Guid? value, string parameterName) =>
        value is null || value.Value != Guid.Empty
            ? value
            : throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);

    public static DateTimeOffset RequireTimestamp(DateTimeOffset value, string parameterName) =>
        DomainEventGuards.RequireOccurredAtUtc(value, parameterName);

    public static DateTimeOffset RequireReachedDeadline(
        DateTimeOffset value,
        DateTimeOffset occurredAtUtc,
        string parameterName)
    {
        DateTimeOffset deadline = RequireTimestamp(value, parameterName);
        return deadline <= occurredAtUtc
            ? deadline
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} cannot be later than the event occurrence time.");
    }

    public static string RequireSubjectId(string value, string parameterName)
    {
        Result<OrganizationSubjectId> subjectId = OrganizationSubjectId.Create(value);
        return subjectId.IsFailure
            ? throw new ArgumentException("Subject id is invalid.", parameterName)
            : subjectId.Value.Value;
    }

    public static int RequireNonNegative(int value, string parameterName) =>
        value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} cannot be negative.");

    public static long RequirePositiveVersion(long value, string parameterName) =>
        value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} must be greater than zero.");

    public static TEnum RequireKnown<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) && !EqualityComparer<TEnum>.Default.Equals(value, default)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} is invalid.");
}
