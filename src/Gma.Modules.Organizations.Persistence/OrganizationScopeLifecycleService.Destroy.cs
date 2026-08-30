namespace Gma.Modules.Organizations.Persistence;

using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ContractDestroyReceipt =
    Contracts.OrganizationScopeDestroyReceipt;
using DomainDestroyOperation =
    Domain.Entities.OrganizationScopeDestroyOperation;
using DomainDestroyReceipt = Domain.Entities.OrganizationScopeDestroyReceipt;
using DomainDestroyStage = Domain.Entities.OrganizationScopeDestroyStage;

internal sealed partial class OrganizationScopeLifecycleService
{
    public async Task<OrganizationScopeDestroyResult> DestroyBatchAsync(
        OrganizationScopeDestroyRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            request.OperationId == Guid.Empty ||
            request.OrganizationId == Guid.Empty ||
            request.ExpectedRevision < 0 ||
            request.ExpectedRevision == long.MaxValue ||
            request.BatchSize is < 1 or >
                OrganizationScopeLifecycleLimits.MaximumDestroyBatchSize)
        {
            return DestroyResult(OrganizationScopeDestroyStatus.Invalid);
        }

        string requestSha256 = DestroyRequestSha256(request);
        await using IDbContextTransaction? transaction =
            await this.BeginSerializableTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
        try
        {
            if (dbContext.Database.IsRelational())
            {
                await OrganizationScopeExistenceTransactionLock.AcquireAsync(
                        dbContext,
                        request.OrganizationId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            DomainDestroyReceipt? existingReceipt = await dbContext
                .OrganizationScopeDestroyReceipts
                .SingleOrDefaultAsync(
                    receipt =>
                        receipt.OrganizationId == request.OrganizationId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existingReceipt is not null)
            {
                OrganizationScopeDestroyResult replay = existingReceipt.Matches(
                    request.OperationId,
                    requestSha256)
                    ? DestroyResult(
                        OrganizationScopeDestroyStatus.Replayed,
                        receipt: Map(existingReceipt))
                    : DestroyResult(OrganizationScopeDestroyStatus.Conflict);
                await CommitAsync(transaction, cancellationToken)
                    .ConfigureAwait(false);
                return replay;
            }

            DomainDestroyOperation? operation = await dbContext
                .OrganizationScopeDestroyOperations
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (operation is not null &&
                !operation.Matches(request.OperationId, requestSha256))
            {
                await RollbackAsync(transaction).ConfigureAwait(false);
                return DestroyResult(OrganizationScopeDestroyStatus.Conflict);
            }

            OrganizationScopeState? state = await dbContext
                .OrganizationScopeStates
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId,
                    cancellationToken)
                .ConfigureAwait(false);
            bool activeWorkChecked = false;
            if (operation is null)
            {
                if (state is null && request.ExpectedRevision != 0)
                {
                    await RollbackAsync(transaction).ConfigureAwait(false);
                    return DestroyResult(OrganizationScopeDestroyStatus.Stale);
                }

                if (state is not null &&
                    state.Version != request.ExpectedRevision)
                {
                    await RollbackAsync(transaction).ConfigureAwait(false);
                    return DestroyResult(OrganizationScopeDestroyStatus.Stale);
                }

                if (state?.IsClosed == true)
                {
                    await RollbackAsync(transaction).ConfigureAwait(false);
                    return DestroyResult(OrganizationScopeDestroyStatus.Conflict);
                }

                if (await this.HasActiveScopeWorkAsync(
                        request.OrganizationId,
                        cancellationToken).ConfigureAwait(false))
                {
                    await RollbackAsync(transaction).ConfigureAwait(false);
                    return DestroyResult(OrganizationScopeDestroyStatus.Busy);
                }

                activeWorkChecked = true;
                if (state is null)
                {
                    state = OrganizationScopeState.Create(
                        request.OrganizationId).Value;
                    await dbContext.OrganizationScopeStates.AddAsync(
                            state,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                DateTimeOffset startedAtUtc = clock.UtcNow;
                if (state.Close(
                        request.OperationId,
                        requestSha256,
                        startedAtUtc) !=
                    OrganizationScopeCloseTransition.Completed)
                {
                    await RollbackAsync(transaction).ConfigureAwait(false);
                    return DestroyResult(OrganizationScopeDestroyStatus.Conflict);
                }

                operation = DomainDestroyOperation.Create(
                    request.OrganizationId,
                    request.OperationId,
                    requestSha256,
                    request.ExpectedRevision,
                    state.Version,
                    request.BatchSize,
                    OrganizationScopeLifecycleLimits.MaximumDestroyBatchSize,
                    startedAtUtc).Value;
                await dbContext.OrganizationScopeDestroyOperations.AddAsync(
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!Matches(operation, state, requestSha256))
            {
                await RollbackAsync(transaction).ConfigureAwait(false);
                return DestroyResult(OrganizationScopeDestroyStatus.Conflict);
            }

            if (!activeWorkChecked &&
                await this.HasActiveScopeWorkAsync(
                    request.OrganizationId,
                    cancellationToken).ConfigureAwait(false))
            {
                await RollbackAsync(transaction).ConfigureAwait(false);
                return DestroyResult(
                    OrganizationScopeDestroyStatus.Busy,
                    Map(operation));
            }

            while (!operation.IsComplete)
            {
                DestroyRecordKey[] loadedKeys = await this.LoadStageKeysAsync(
                        request.OrganizationId,
                        operation.Stage,
                        operation.BatchSize + 1,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (loadedKeys.Length == 0)
                {
                    if (!operation.AdvanceEmptyStage(clock.UtcNow))
                    {
                        throw new InvalidDataException(
                            "Organization scope destruction stage progress is invalid.");
                    }

                    continue;
                }

                DestroyRecordKey[] selectedKeys = loadedKeys
                    .Take(operation.BatchSize)
                    .ToArray();
                int removed = await this.DeleteStageKeysAsync(
                        request.OrganizationId,
                        operation.Stage,
                        selectedKeys,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (removed != selectedKeys.Length ||
                    !operation.RecordBatch(
                        operation.Stage,
                        removed,
                        KeysSha256(selectedKeys),
                        stageCompleted:
                            loadedKeys.Length <= operation.BatchSize,
                        clock.UtcNow))
                {
                    throw new InvalidDataException(
                        "Organization scope destruction batch progress is invalid.");
                }

                break;
            }

            if (!operation.IsComplete)
            {
                await dbContext.SaveScopeDestructionChangesAsync(
                        request.OrganizationId,
                        request.OperationId,
                        cancellationToken)
                    .ConfigureAwait(false);
                await CommitAsync(transaction, cancellationToken)
                    .ConfigureAwait(false);
                return DestroyResult(
                    OrganizationScopeDestroyStatus.InProgress,
                    Map(operation));
            }

            DomainDestroyReceipt receipt = DomainDestroyReceipt.Create(
                operation,
                clock.UtcNow).Value;
            await dbContext.OrganizationScopeDestroyReceipts.AddAsync(
                    receipt,
                    cancellationToken)
                .ConfigureAwait(false);
            dbContext.OrganizationScopeDestroyOperations.Remove(operation);
            await dbContext.SaveScopeDestructionChangesAsync(
                    request.OrganizationId,
                    request.OperationId,
                    cancellationToken)
                .ConfigureAwait(false);
            await CommitAsync(transaction, cancellationToken)
                .ConfigureAwait(false);
            return DestroyResult(
                OrganizationScopeDestroyStatus.Completed,
                receipt: Map(receipt));
        }
        catch (DbUpdateException exception) when (
            transaction is not null &&
            IsConcurrentPostgreSqlScopeStateCreation(
                exception,
                request,
                dbContext))
        {
            await RollbackAsync(transaction).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return DestroyResult(OrganizationScopeDestroyStatus.Stale);
        }
        catch
        {
            await RollbackAsync(transaction).ConfigureAwait(false);
            throw;
        }
    }

    private Task<bool> HasActiveScopeWorkAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        string scopeId = organizationId.ToString("D");
        DateTimeOffset nowUtc = clock.UtcNow;
        return dbContext.OutboxMessages.AnyAsync(
            message =>
                message.ScopeId == scopeId &&
                message.ProcessedAtUtc == null &&
                message.LockedUntilUtc > nowUtc,
            cancellationToken);
    }

    private Task<DestroyRecordKey[]> LoadStageKeysAsync(
        Guid organizationId,
        DomainDestroyStage stage,
        int take,
        CancellationToken cancellationToken)
    {
        string scopeId = organizationId.ToString("D");
        return stage switch
        {
            DomainDestroyStage.InboxMessages => dbContext.InboxMessages
                .Where(message => message.ScopeId == scopeId)
                .OrderBy(message => message.Id)
                .ThenBy(message => message.Handler)
                .Select(message => new DestroyRecordKey(
                    message.Id,
                    message.Handler))
                .Take(take)
                .ToArrayAsync(cancellationToken),
            DomainDestroyStage.OutboxMessages => dbContext.OutboxMessages
                .Where(message => message.ScopeId == scopeId)
                .OrderBy(message => message.Id)
                .Select(message => new DestroyRecordKey(message.Id, null))
                .Take(take)
                .ToArrayAsync(cancellationToken),
            DomainDestroyStage.EnrollmentClaims => dbContext.EnrollmentClaims
                .Where(claim => claim.OrganizationId == organizationId)
                .OrderBy(claim => claim.Id)
                .Select(claim => new DestroyRecordKey(claim.Id, null))
                .Take(take)
                .ToArrayAsync(cancellationToken),
            DomainDestroyStage.Invitations => dbContext.Invitations
                .Where(invitation =>
                    invitation.OrganizationId == organizationId)
                .OrderBy(invitation => invitation.Id)
                .Select(invitation => new DestroyRecordKey(
                    invitation.Id,
                    null))
                .Take(take)
                .ToArrayAsync(cancellationToken),
            DomainDestroyStage.EnrollmentLinks => dbContext.EnrollmentLinks
                .Where(link => link.OrganizationId == organizationId)
                .OrderBy(link => link.Id)
                .Select(link => new DestroyRecordKey(link.Id, null))
                .Take(take)
                .ToArrayAsync(cancellationToken),
            DomainDestroyStage.Memberships => dbContext.Memberships
                .Where(membership =>
                    membership.OrganizationId == organizationId)
                .OrderBy(membership => membership.Id)
                .Select(membership => new DestroyRecordKey(
                    membership.Id,
                    null))
                .Take(take)
                .ToArrayAsync(cancellationToken),
            DomainDestroyStage.Organization => dbContext.Organizations
                .Where(organization => organization.Id == organizationId)
                .OrderBy(organization => organization.Id)
                .Select(organization => new DestroyRecordKey(
                    organization.Id,
                    null))
                .Take(1)
                .ToArrayAsync(cancellationToken),
            _ => throw new InvalidOperationException(
                "The organization scope destruction stage is invalid.")
        };
    }

    private async Task<int> DeleteStageKeysAsync(
        Guid organizationId,
        DomainDestroyStage stage,
        DestroyRecordKey[] keys,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            return await this.DeleteRelationalStageKeysAsync(
                    organizationId,
                    stage,
                    keys.Length,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        string scopeId = organizationId.ToString("D");
        Guid[] ids = keys.Select(key => key.Id).Distinct().ToArray();
        switch (stage)
        {
            case DomainDestroyStage.InboxMessages:
            {
                InboxMessage[] candidates = await dbContext.InboxMessages
                    .Where(message =>
                        message.ScopeId == scopeId &&
                        ids.Contains(message.Id))
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
                HashSet<DestroyRecordKey> selected = keys.ToHashSet();
                dbContext.InboxMessages.RemoveRange(candidates.Where(message =>
                    selected.Contains(new DestroyRecordKey(
                        message.Id,
                        message.Handler))));
                break;
            }
            case DomainDestroyStage.OutboxMessages:
                dbContext.OutboxMessages.RemoveRange(await dbContext
                    .OutboxMessages
                    .Where(message =>
                        message.ScopeId == scopeId &&
                        ids.Contains(message.Id))
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false));
                break;
            case DomainDestroyStage.EnrollmentClaims:
                dbContext.EnrollmentClaims.RemoveRange(await dbContext
                    .EnrollmentClaims
                    .Where(claim =>
                        claim.OrganizationId == organizationId &&
                        ids.Contains(claim.Id))
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false));
                break;
            case DomainDestroyStage.Invitations:
                dbContext.Invitations.RemoveRange(await dbContext.Invitations
                    .Where(invitation =>
                        invitation.OrganizationId == organizationId &&
                        ids.Contains(invitation.Id))
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false));
                break;
            case DomainDestroyStage.EnrollmentLinks:
                dbContext.EnrollmentLinks.RemoveRange(await dbContext
                    .EnrollmentLinks
                    .Where(link =>
                        link.OrganizationId == organizationId &&
                        ids.Contains(link.Id))
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false));
                break;
            case DomainDestroyStage.Memberships:
                dbContext.Memberships.RemoveRange(await dbContext.Memberships
                    .Where(membership =>
                        membership.OrganizationId == organizationId &&
                        ids.Contains(membership.Id))
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false));
                break;
            case DomainDestroyStage.Organization:
                dbContext.Organizations.RemoveRange(await dbContext.Organizations
                    .Where(organization =>
                        organization.Id == organizationId &&
                        ids.Contains(organization.Id))
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false));
                break;
            case DomainDestroyStage.Unknown:
            case DomainDestroyStage.Completed:
            default:
                throw new InvalidOperationException(
                    "The organization scope destruction stage is invalid.");
        }

        return keys.Length;
    }

    private async Task<int> DeleteRelationalStageKeysAsync(
        Guid organizationId,
        DomainDestroyStage stage,
        int count,
        CancellationToken cancellationToken)
    {
        string scopeId = organizationId.ToString("D");
        return stage switch
        {
            DomainDestroyStage.InboxMessages => await dbContext.InboxMessages
                .Where(message => message.ScopeId == scopeId)
                .OrderBy(message => message.Id)
                .ThenBy(message => message.Handler)
                .Take(count)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false),
            DomainDestroyStage.OutboxMessages => await dbContext.OutboxMessages
                .Where(message => message.ScopeId == scopeId)
                .OrderBy(message => message.Id)
                .Take(count)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false),
            DomainDestroyStage.EnrollmentClaims => await dbContext
                .EnrollmentClaims
                .Where(claim => claim.OrganizationId == organizationId)
                .OrderBy(claim => claim.Id)
                .Take(count)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false),
            DomainDestroyStage.Invitations => await dbContext.Invitations
                .Where(invitation =>
                    invitation.OrganizationId == organizationId)
                .OrderBy(invitation => invitation.Id)
                .Take(count)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false),
            DomainDestroyStage.EnrollmentLinks => await dbContext.EnrollmentLinks
                .Where(link => link.OrganizationId == organizationId)
                .OrderBy(link => link.Id)
                .Take(count)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false),
            DomainDestroyStage.Memberships => await dbContext.Memberships
                .Where(membership =>
                    membership.OrganizationId == organizationId)
                .OrderBy(membership => membership.Id)
                .Take(count)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false),
            DomainDestroyStage.Organization => await dbContext.Organizations
                .Where(organization => organization.Id == organizationId)
                .Take(1)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException(
                "The organization scope destruction stage is invalid.")
        };
    }

    private async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational() ||
            dbContext.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task RollbackAsync(
        IDbContextTransaction? transaction)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static bool IsConcurrentPostgreSqlScopeStateCreation(
        DbUpdateException exception,
        OrganizationScopeDestroyRequest request,
        OrganizationsDbContext dbContext)
    {
        if (request.ExpectedRevision != 0 ||
            !dbContext.ChangeTracker.Entries<OrganizationScopeState>().Any(
                entry => entry.State == EntityState.Added &&
                    entry.Entity.OrganizationId == request.OrganizationId))
        {
            return false;
        }

        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    SchemaName: OrganizationsMigrations.Schema,
                    TableName: "organization_scope_states",
                    ConstraintName: "PK_organization_scope_states"
                })
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(
        DomainDestroyOperation operation,
        OrganizationScopeState? state,
        string requestSha256) =>
        state is not null &&
        state.IsClosed &&
        state.CloseOperationId == operation.OperationId &&
        state.Version == operation.ResultingRevision &&
        string.Equals(
            state.CloseRequestSha256,
            requestSha256,
            StringComparison.Ordinal);

    private static string DestroyRequestSha256(
        OrganizationScopeDestroyRequest request) =>
        Sha256(
            "gma-organization-scope-destroy/v1|" +
            $"{request.OrganizationId:D}|{request.ExpectedRevision}|" +
            request.BatchSize.ToString(CultureInfo.InvariantCulture));

    private static string KeysSha256(IEnumerable<DestroyRecordKey> keys) =>
        Sha256(string.Join(
            '\n',
            keys.OrderBy(key => key.Id)
                .ThenBy(key => key.Discriminator, StringComparer.Ordinal)
                .Select(key => key.Canonical)));

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static OrganizationScopeDestroyProgress Map(
        DomainDestroyOperation operation) =>
        new(
            operation.OperationId,
            operation.ResultingRevision,
            operation.BatchSize,
            ToContract(operation.Stage),
            operation.RemovedRecordCount,
            operation.CompletedBatchCount,
            operation.ProofVersion,
            operation.RemovalProofSha256,
            operation.StartedAtUtc,
            operation.UpdatedAtUtc);

    private static ContractDestroyReceipt Map(DomainDestroyReceipt receipt) =>
        new(
            receipt.OperationId,
            receipt.ResultingRevision,
            receipt.BatchSize,
            receipt.RemovedRecordCount,
            receipt.CompletedBatchCount,
            receipt.RemovalProofVersion,
            receipt.RemovalProofSha256,
            receipt.StartedAtUtc,
            receipt.CompletedAtUtc);

    private static OrganizationScopeDestructionStage ToContract(
        DomainDestroyStage stage) =>
        stage switch
        {
            DomainDestroyStage.InboxMessages =>
                OrganizationScopeDestructionStage.InboxMessages,
            DomainDestroyStage.OutboxMessages =>
                OrganizationScopeDestructionStage.OutboxMessages,
            DomainDestroyStage.EnrollmentClaims =>
                OrganizationScopeDestructionStage.EnrollmentClaims,
            DomainDestroyStage.Invitations =>
                OrganizationScopeDestructionStage.Invitations,
            DomainDestroyStage.EnrollmentLinks =>
                OrganizationScopeDestructionStage.EnrollmentLinks,
            DomainDestroyStage.Memberships =>
                OrganizationScopeDestructionStage.Memberships,
            DomainDestroyStage.Organization =>
                OrganizationScopeDestructionStage.Organization,
            DomainDestroyStage.Completed =>
                OrganizationScopeDestructionStage.Completed,
            _ => OrganizationScopeDestructionStage.Unknown
        };

    private static OrganizationScopeDestroyResult DestroyResult(
        OrganizationScopeDestroyStatus status,
        OrganizationScopeDestroyProgress? progress = null,
        ContractDestroyReceipt? receipt = null) =>
        new(status, progress, receipt);

    private sealed record DestroyRecordKey(Guid Id, string? Discriminator)
    {
        public string Canonical => this.Discriminator is null
            ? this.Id.ToString("D") + "|-"
            : this.Id.ToString("D") + "|" +
              this.Discriminator.Length.ToString(CultureInfo.InvariantCulture) +
              ':' + this.Discriminator;
    }
}
