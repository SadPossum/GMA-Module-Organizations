namespace Gma.Modules.Organizations.Domain.ValueObjects;

using System.Net.Mail;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

public sealed record OrganizationInvitationRecipient
{
    public const int MaxLength = 320;

    private OrganizationInvitationRecipient(string? email) => this.Email = email;

    public string? Email { get; }

    public static Result<OrganizationInvitationRecipient> Create(string? value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return Result.Success(new OrganizationInvitationRecipient((string?)null));
        }

        if (normalized.Length > MaxLength)
        {
            return Result.Failure<OrganizationInvitationRecipient>(
                OrganizationDomainErrors.InvitationRecipientInvalid);
        }

        try
        {
            MailAddress parsed = new(normalized);
            return string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase)
                ? Result.Success(new OrganizationInvitationRecipient(parsed.Address.ToLowerInvariant()))
                : Result.Failure<OrganizationInvitationRecipient>(OrganizationDomainErrors.InvitationRecipientInvalid);
        }
        catch (FormatException)
        {
            return Result.Failure<OrganizationInvitationRecipient>(
                OrganizationDomainErrors.InvitationRecipientInvalid);
        }
    }
}
