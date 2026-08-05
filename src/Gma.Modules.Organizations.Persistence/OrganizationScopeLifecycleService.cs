namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed partial class OrganizationScopeLifecycleService(
    OrganizationsDbContext dbContext,
    ISystemClock clock)
    : IOrganizationScopeLifecycle
{
    private const string IdCursorPrefix = "id:";

    public async Task<OrganizationScopeSnapshot> GetSnapshotAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return new OrganizationScopeSnapshot(
                OrganizationScopeStatus.Invalid,
                0);
        }

        ScopeStateSnapshot? state = await this.ReadStateAsync(
                organizationId,
                cancellationToken)
            .ConfigureAwait(false);
        return state switch
        {
            null => new OrganizationScopeSnapshot(
                OrganizationScopeStatus.Missing,
                0),
            { IsClosed: true } => new OrganizationScopeSnapshot(
                OrganizationScopeStatus.Closed,
                state.Version),
            _ => new OrganizationScopeSnapshot(
                OrganizationScopeStatus.Open,
                state.Version)
        };
    }

    public async Task<OrganizationScopeExportPage> ExportAsync(
        OrganizationScopeExportRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            request.OrganizationId == Guid.Empty ||
            request.ExpectedRevision < 0 ||
            request.ExpectedRevision == long.MaxValue ||
            request.PageSize is < 1 or >
                OrganizationScopeLifecycleLimits.MaximumPageSize ||
            request.Store is <= OrganizationScopeExportStore.Unknown or >
                OrganizationScopeExportStore.EnrollmentClaims ||
            (request.AfterCursor is not null &&
             (request.AfterCursor.Length == 0 ||
              request.AfterCursor.Length >
                OrganizationScopeLifecycleLimits.MaximumCursorLength ||
              request.AfterCursor.Any(char.IsControl))))
        {
            return EmptyPage(
                OrganizationScopeExportStatus.Invalid,
                0,
                request?.Store ?? OrganizationScopeExportStore.Unknown);
        }

        ScopeStateSnapshot? state = await this.ReadStateAsync(
                request.OrganizationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (state is null)
        {
            return EmptyPage(
                request.ExpectedRevision == 0
                    ? OrganizationScopeExportStatus.Missing
                    : OrganizationScopeExportStatus.Stale,
                0,
                request.Store);
        }

        if (state.IsClosed)
        {
            return EmptyPage(
                OrganizationScopeExportStatus.Closed,
                state.Version,
                request.Store);
        }

        if (state.Version != request.ExpectedRevision)
        {
            return EmptyPage(
                OrganizationScopeExportStatus.Stale,
                state.Version,
                request.Store);
        }

        if (!TryParseIdCursor(request.AfterCursor, out Guid? afterId))
        {
            return EmptyPage(
                OrganizationScopeExportStatus.Invalid,
                request.ExpectedRevision,
                request.Store);
        }

        return request.Store switch
        {
            OrganizationScopeExportStore.Organization =>
                await this.ExportOrganizationAsync(
                    request,
                    afterId,
                    cancellationToken).ConfigureAwait(false),
            OrganizationScopeExportStore.Memberships =>
                await this.ExportMembershipsAsync(
                    request,
                    afterId,
                    cancellationToken).ConfigureAwait(false),
            OrganizationScopeExportStore.Invitations =>
                await this.ExportInvitationsAsync(
                    request,
                    afterId,
                    cancellationToken).ConfigureAwait(false),
            OrganizationScopeExportStore.EnrollmentLinks =>
                await this.ExportEnrollmentLinksAsync(
                    request,
                    afterId,
                    cancellationToken).ConfigureAwait(false),
            OrganizationScopeExportStore.EnrollmentClaims =>
                await this.ExportEnrollmentClaimsAsync(
                    request,
                    afterId,
                    cancellationToken).ConfigureAwait(false),
            _ => EmptyPage(
                OrganizationScopeExportStatus.Invalid,
                request.ExpectedRevision,
                request.Store)
        };
    }

    private async Task<OrganizationScopeExportPage> ExportOrganizationAsync(
        OrganizationScopeExportRequest request,
        Guid? afterId,
        CancellationToken cancellationToken)
    {
        IQueryable<Organization> query = dbContext.Organizations
            .Where(organization =>
                organization.Id == request.OrganizationId);
        if (afterId.HasValue)
        {
            Guid cursor = afterId.Value;
            query = query.Where(organization =>
                organization.Id.CompareTo(cursor) > 0);
        }

        Organization[] loaded = await query
            .AsNoTracking()
            .OrderBy(organization => organization.Id)
            .Take(request.PageSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return await this.CompleteGuidPageAsync(
                request,
                loaded,
                organization => organization.Id,
                Map,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OrganizationScopeExportPage> ExportMembershipsAsync(
        OrganizationScopeExportRequest request,
        Guid? afterId,
        CancellationToken cancellationToken)
    {
        IQueryable<OrganizationMembership> query = dbContext.Memberships
            .Where(membership =>
                membership.OrganizationId == request.OrganizationId);
        if (afterId.HasValue)
        {
            Guid cursor = afterId.Value;
            query = query.Where(membership =>
                membership.Id.CompareTo(cursor) > 0);
        }

        OrganizationMembership[] loaded = await query
            .AsNoTracking()
            .OrderBy(membership => membership.Id)
            .Take(request.PageSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return await this.CompleteGuidPageAsync(
                request,
                loaded,
                membership => membership.Id,
                Map,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OrganizationScopeExportPage> ExportInvitationsAsync(
        OrganizationScopeExportRequest request,
        Guid? afterId,
        CancellationToken cancellationToken)
    {
        IQueryable<OrganizationInvitation> query = dbContext.Invitations
            .Where(invitation =>
                invitation.OrganizationId == request.OrganizationId);
        if (afterId.HasValue)
        {
            Guid cursor = afterId.Value;
            query = query.Where(invitation =>
                invitation.Id.CompareTo(cursor) > 0);
        }

        OrganizationInvitation[] loaded = await query
            .AsNoTracking()
            .OrderBy(invitation => invitation.Id)
            .Take(request.PageSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return await this.CompleteGuidPageAsync(
                request,
                loaded,
                invitation => invitation.Id,
                Map,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OrganizationScopeExportPage> ExportEnrollmentLinksAsync(
        OrganizationScopeExportRequest request,
        Guid? afterId,
        CancellationToken cancellationToken)
    {
        IQueryable<OrganizationEnrollmentLink> query = dbContext.EnrollmentLinks
            .Where(link => link.OrganizationId == request.OrganizationId);
        if (afterId.HasValue)
        {
            Guid cursor = afterId.Value;
            query = query.Where(link => link.Id.CompareTo(cursor) > 0);
        }

        OrganizationEnrollmentLink[] loaded = await query
            .AsNoTracking()
            .OrderBy(link => link.Id)
            .Take(request.PageSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return await this.CompleteGuidPageAsync(
                request,
                loaded,
                link => link.Id,
                Map,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OrganizationScopeExportPage> ExportEnrollmentClaimsAsync(
        OrganizationScopeExportRequest request,
        Guid? afterId,
        CancellationToken cancellationToken)
    {
        IQueryable<OrganizationEnrollmentClaim> query = dbContext
            .EnrollmentClaims
            .Where(claim => claim.OrganizationId == request.OrganizationId);
        if (afterId.HasValue)
        {
            Guid cursor = afterId.Value;
            query = query.Where(claim => claim.Id.CompareTo(cursor) > 0);
        }

        OrganizationEnrollmentClaim[] loaded = await query
            .AsNoTracking()
            .OrderBy(claim => claim.Id)
            .Take(request.PageSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return await this.CompleteGuidPageAsync(
                request,
                loaded,
                claim => claim.Id,
                Map,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OrganizationScopeExportPage> CompleteGuidPageAsync<T>(
        OrganizationScopeExportRequest request,
        IReadOnlyList<T> loaded,
        Func<T, Guid> id,
        Func<T, OrganizationScopeExportRecord> map,
        CancellationToken cancellationToken)
    {
        bool hasMore = loaded.Count > request.PageSize;
        T[] selected = loaded.Take(request.PageSize).ToArray();
        string? nextCursor = selected.Length == 0
            ? request.AfterCursor
            : IdCursor(id(selected[^1]));
        ScopeStateSnapshot? current = await this.ReadStateAsync(
                request.OrganizationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (current is null || current.Version != request.ExpectedRevision)
        {
            return EmptyPage(
                OrganizationScopeExportStatus.Stale,
                current?.Version ?? 0,
                request.Store);
        }

        if (current.IsClosed)
        {
            return EmptyPage(
                OrganizationScopeExportStatus.Closed,
                current.Version,
                request.Store);
        }

        return new OrganizationScopeExportPage(
            OrganizationScopeExportStatus.Completed,
            current.Version,
            request.Store,
            selected.Select(map).ToArray(),
            nextCursor,
            hasMore);
    }

    private async Task<ScopeStateSnapshot?> ReadStateAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        ScopeStateSnapshot? state = await dbContext.OrganizationScopeStates
            .AsNoTracking()
            .Where(candidate => candidate.OrganizationId == organizationId)
            .Select(candidate => new ScopeStateSnapshot(
                candidate.Version,
                candidate.IsClosed))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (state is not null)
        {
            return state;
        }

        return await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(
                organization => organization.Id == organizationId,
                cancellationToken)
            .ConfigureAwait(false)
            ? new ScopeStateSnapshot(0, IsClosed: false)
            : null;
    }

    private static bool TryParseIdCursor(
        string? cursor,
        out Guid? afterId)
    {
        afterId = null;
        if (cursor is null)
        {
            return true;
        }

        if (!cursor.StartsWith(IdCursorPrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(
                cursor[IdCursorPrefix.Length..],
                "D",
                out Guid parsed) ||
            parsed == Guid.Empty)
        {
            return false;
        }

        afterId = parsed;
        return true;
    }

    private static string IdCursor(Guid id) =>
        IdCursorPrefix + id.ToString("D");

    private static OrganizationScopeExportPage EmptyPage(
        OrganizationScopeExportStatus status,
        long revision,
        OrganizationScopeExportStore store) =>
        new(status, revision, store, [], null, false);

    private sealed record ScopeStateSnapshot(long Version, bool IsClosed);
}
