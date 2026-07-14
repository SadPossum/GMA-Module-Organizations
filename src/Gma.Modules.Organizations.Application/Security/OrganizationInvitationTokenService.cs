namespace Gma.Modules.Organizations.Application.Security;

using System.Security.Cryptography;
using System.Text;
using Gma.Modules.Organizations.Application.Ports;

internal sealed class OrganizationInvitationTokenService : IOrganizationInvitationTokenService
{
    private const string Purpose = "gma-organizations:invitation:v1:";
    private const int TokenBytes = 32;

    public IssuedOrganizationInvitationToken Issue()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        string secret = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new IssuedOrganizationInvitationToken(secret, this.ComputeDigest(secret));
    }

    public bool IsWellFormed(string? secret) => secret is { Length: 43 } &&
        secret.All(character =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');

    public string ComputeDigest(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(Purpose + secret));
        return Convert.ToHexStringLower(digest);
    }

    public bool Verify(string secret, string expectedDigest)
    {
        if (!this.IsWellFormed(secret) || expectedDigest is not { Length: 64 } ||
            !expectedDigest.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
        {
            return false;
        }

        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(Purpose + secret));
        byte[] expected = Convert.FromHexString(expectedDigest);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
