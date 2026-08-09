namespace Gma.Modules.Organizations.Persistence.Repositories;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.Organizations.Application.Ports;

internal sealed class OrganizationJoinSubjectCoordinator(
    OrganizationsDbContext dbContext) : IOrganizationJoinSubjectCoordinator
{
    public Task AcquireAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization id is required for join-subject coordination.",
                nameof(organizationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        string normalizedSubject = subjectId.Trim();
        string subjectDigest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSubject)));

        return EfTransactionKeyLock.AcquireAsync(
            dbContext,
            $"gma:organizations:join-subject:{organizationId:N}:{subjectDigest}",
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);
    }
}
