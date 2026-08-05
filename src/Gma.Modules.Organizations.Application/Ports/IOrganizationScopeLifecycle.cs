namespace Gma.Modules.Organizations.Application.Ports;

using Gma.Modules.Organizations.Contracts;

public interface IOrganizationScopeLifecycle
{
    Task<OrganizationScopeSnapshot> GetSnapshotAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<OrganizationScopeExportPage> ExportAsync(
        OrganizationScopeExportRequest request,
        CancellationToken cancellationToken);

    Task<OrganizationScopeDestroyResult> DestroyBatchAsync(
        OrganizationScopeDestroyRequest request,
        CancellationToken cancellationToken);
}

public sealed record OrganizationScopeSnapshot(
    OrganizationScopeStatus Status,
    long Revision);

public enum OrganizationScopeStatus
{
    Invalid = 0,
    Missing = 1,
    Open = 2,
    Closed = 3
}

public sealed record OrganizationScopeExportRequest(
    Guid OrganizationId,
    long ExpectedRevision,
    OrganizationScopeExportStore Store,
    string? AfterCursor,
    int PageSize);

public sealed record OrganizationScopeExportPage(
    OrganizationScopeExportStatus Status,
    long ScopeRevision,
    OrganizationScopeExportStore Store,
    IReadOnlyList<OrganizationScopeExportRecord> Records,
    string? NextCursor,
    bool HasMore);

public enum OrganizationScopeExportStatus
{
    Invalid = 0,
    Completed = 1,
    Missing = 2,
    Closed = 3,
    Stale = 4
}

public enum OrganizationScopeExportStore
{
    Unknown = 0,
    Organization = 1,
    Memberships = 2,
    Invitations = 3,
    EnrollmentLinks = 4,
    EnrollmentClaims = 5
}

public static class OrganizationScopeLifecycleLimits
{
    public const int MaximumPageSize = 200;
    public const int MaximumCursorLength = 80;
    public const int MaximumDestroyBatchSize = 1_000;
}

public sealed record OrganizationScopeDestroyRequest(
    Guid OperationId,
    Guid OrganizationId,
    long ExpectedRevision,
    int BatchSize);

public sealed record OrganizationScopeDestroyResult(
    OrganizationScopeDestroyStatus Status,
    OrganizationScopeDestroyProgress? Progress,
    OrganizationScopeDestroyReceipt? Receipt);

public enum OrganizationScopeDestroyStatus
{
    Invalid = 0,
    InProgress = 1,
    Completed = 2,
    Replayed = 3,
    Stale = 4,
    Busy = 5,
    Conflict = 6
}

public enum OrganizationScopeDestructionStage
{
    Unknown = 0,
    InboxMessages = 1,
    OutboxMessages = 2,
    EnrollmentClaims = 3,
    Invitations = 4,
    EnrollmentLinks = 5,
    Memberships = 6,
    Organization = 7,
    Completed = 8
}

public sealed record OrganizationScopeDestroyProgress(
    Guid OperationId,
    long ResultingRevision,
    int BatchSize,
    OrganizationScopeDestructionStage Stage,
    long RemovedRecordCount,
    int CompletedBatchCount,
    int RemovalProofVersion,
    string RemovalProofSha256,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record OrganizationScopeDestroyReceipt(
    Guid OperationId,
    long ResultingRevision,
    int BatchSize,
    long RemovedRecordCount,
    int CompletedBatchCount,
    int RemovalProofVersion,
    string RemovalProofSha256,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public abstract record OrganizationScopeExportRecord;

public sealed record OrganizationScopeOrganizationExportRecord(
    Guid OrganizationId,
    string Name,
    string Slug,
    OrganizationStatus Status,
    int ActiveOwnerCount,
    long Version,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc)
    : OrganizationScopeExportRecord;

public sealed record OrganizationScopeMembershipExportRecord(
    Guid MembershipId,
    Guid OrganizationId,
    string SubjectId,
    OrganizationMembershipRole Role,
    OrganizationMembershipStatus Status,
    long Version,
    string CreatedBy,
    DateTimeOffset JoinedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc)
    : OrganizationScopeExportRecord;

public sealed record OrganizationScopeInvitationExportRecord(
    Guid InvitationId,
    Guid OrganizationId,
    string InviterSubjectId,
    string? RecipientEmail,
    int TokenVersion,
    DateTimeOffset ExpiresAtUtc,
    OrganizationInvitationStatus Status,
    string? AcceptedSubjectId,
    Guid? AcceptedMembershipId,
    DateTimeOffset? AcceptedAtUtc,
    long Version,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc)
    : OrganizationScopeExportRecord;

public sealed record OrganizationScopeEnrollmentLinkExportRecord(
    Guid EnrollmentLinkId,
    Guid OrganizationId,
    string CreatorSubjectId,
    int TokenVersion,
    DateTimeOffset ExpiresAtUtc,
    int MaximumClaims,
    int ReservedClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode,
    OrganizationEnrollmentLinkStatus Status,
    long Version,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc)
    : OrganizationScopeExportRecord;

public sealed record OrganizationScopeEnrollmentClaimExportRecord(
    Guid EnrollmentClaimId,
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    string SubjectId,
    OrganizationEnrollmentClaimStatus Status,
    Guid? MembershipId,
    DateTimeOffset? DecisionExpiresAtUtc,
    long Version,
    DateTimeOffset CreatedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc)
    : OrganizationScopeExportRecord;
