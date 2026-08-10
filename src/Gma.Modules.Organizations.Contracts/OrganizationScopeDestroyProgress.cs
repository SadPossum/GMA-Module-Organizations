namespace Gma.Modules.Organizations.Contracts;

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
