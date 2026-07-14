namespace Gma.Modules.Organizations.Application.Ports;

public sealed record IssuedOrganizationInvitationToken(string Secret, string Digest);

public interface IOrganizationInvitationTokenService
{
    IssuedOrganizationInvitationToken Issue();
    bool IsWellFormed(string? secret);
    string ComputeDigest(string secret);
    bool Verify(string secret, string expectedDigest);
}
