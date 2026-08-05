namespace Gma.Modules.Organizations.Persistence;

using System.Data;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal sealed class OrganizationsOutboxStore(OrganizationsDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<OrganizationsDbContext>(
        dbContext,
        options,
        OrganizationsMigrations.Schema)
{
    protected override IQueryable<OutboxMessage> ApplyClaimAdmission(
        IQueryable<OutboxMessage> candidates) =>
        candidates.Where(message =>
            message.ScopeId == null ||
            !this.DbContext.OrganizationScopeStates.Any(state =>
                state.ScopeId == message.ScopeId && state.IsClosed));

    public override async Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset processedBeforeUtc,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        ValidateCleanupArguments(processedBeforeUtc, maxMessages);
        await using IDbContextTransaction? transaction =
            await this.BeginSerializableTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
        IQueryable<OutboxMessage> candidates = this.DbContext.OutboxMessages
            .Where(message =>
                message.ProcessedAtUtc != null &&
                message.ProcessedAtUtc < processedBeforeUtc)
            .Where(message =>
                message.ScopeId == null ||
                !this.DbContext.OrganizationScopeStates.Any(state =>
                    state.ScopeId == message.ScopeId && state.IsClosed))
            .OrderBy(message => message.ProcessedAtUtc)
            .ThenBy(message => message.Id)
            .Take(maxMessages);
        OutboxCleanupCandidate[] selected = await candidates
            .Select(message => new OutboxCleanupCandidate(
                message.Id,
                message.ScopeId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (selected.Length == 0)
        {
            await CommitAsync(transaction, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }

        Guid[] organizationIds = selected
            .Select(candidate => candidate.ScopeId)
            .Where(scopeId => OrganizationMessageScopes.TryParse(
                scopeId,
                out _))
            .Select(scopeId => Guid.ParseExact(scopeId!, "D"))
            .Distinct()
            .ToArray();
        if (organizationIds.Length > 0)
        {
            if (!await this.DbContext
                    .TryRegisterMaintenanceScopeMutationsAsync(
                        organizationIds,
                        cancellationToken).ConfigureAwait(false))
            {
                throw new OrganizationScopeClosedException();
            }

            await this.DbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        Guid[] selectedIds = selected.Select(candidate => candidate.Id).ToArray();
        int removed = await this.DbContext.OutboxMessages
            .Where(message => selectedIds.Contains(message.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        if (removed != selected.Length)
        {
            throw new InvalidDataException(
                "Organization outbox cleanup changed during its revision fence.");
        }

        await CommitAsync(transaction, cancellationToken)
            .ConfigureAwait(false);
        return removed;
    }

    private async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (!this.DbContext.Database.IsRelational() ||
            this.DbContext.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await this.DbContext.Database.BeginTransactionAsync(
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

    private static void ValidateCleanupArguments(
        DateTimeOffset processedBeforeUtc,
        int maxMessages)
    {
        if (processedBeforeUtc == default)
        {
            throw new ArgumentException(
                $"{nameof(processedBeforeUtc)} must not be the default timestamp.",
                nameof(processedBeforeUtc));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);
    }

    private sealed record OutboxCleanupCandidate(Guid Id, string? ScopeId);
}
