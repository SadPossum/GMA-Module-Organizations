namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;

internal static class OrganizationContractEnumJson
{
    internal delegate bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum;

    public static TEnum ReadString<TEnum>(
        ref Utf8JsonReader reader,
        string displayName,
        TryParse<TEnum> parser)
        where TEnum : struct, Enum
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException($"{displayName} must be a string.");
        }

        return parser(reader.GetString(), out TEnum value)
            ? value
            : throw new JsonException($"{displayName} is invalid.");
    }

    public static void WriteString<TEnum>(
        Utf8JsonWriter writer,
        TEnum value,
        string displayName,
        Func<TEnum, string> formatter)
        where TEnum : struct, Enum
    {
        try
        {
            writer.WriteStringValue(formatter(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException($"{displayName} is invalid.", exception);
        }
    }
}
