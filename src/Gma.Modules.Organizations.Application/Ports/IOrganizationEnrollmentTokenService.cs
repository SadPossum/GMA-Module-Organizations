namespace Gma.Modules.Organizations.Application.Ports;

public sealed record IssuedOrganizationEnrollmentToken(string Secret, string Digest);

public interface IOrganizationEnrollmentTokenService
{
    IssuedOrganizationEnrollmentToken Issue();
    bool IsWellFormed(string? secret);
    string ComputeDigest(string secret);
    bool Verify(string secret, string expectedDigest);
}
