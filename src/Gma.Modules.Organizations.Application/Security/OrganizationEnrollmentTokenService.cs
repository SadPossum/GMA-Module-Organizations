namespace Gma.Modules.Organizations.Application.Security;

using System.Security.Cryptography;
using System.Text;
using Gma.Modules.Organizations.Application.Ports;

internal sealed class OrganizationEnrollmentTokenService : IOrganizationEnrollmentTokenService
{
    private const string Purpose = "gma-organizations:enrollment:v1:";

    public IssuedOrganizationEnrollmentToken Issue()
    {
        string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new IssuedOrganizationEnrollmentToken(secret, this.ComputeDigest(secret));
    }

    public bool IsWellFormed(string? secret) => secret is { Length: 43 } &&
        secret.All(character =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');

    public string ComputeDigest(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Purpose + secret)));
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
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expectedDigest));
    }
}
