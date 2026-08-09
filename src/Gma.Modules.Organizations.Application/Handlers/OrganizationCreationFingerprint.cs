namespace Gma.Modules.Organizations.Application.Handlers;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

internal static class OrganizationCreationFingerprint
{
    public static string Compute(
        string name,
        string slug,
        string subjectId,
        string actorId)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (string value in new[]
                 {
                     "gma-organizations-create/v1",
                     name,
                     slug,
                     subjectId,
                     actorId
                 })
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
