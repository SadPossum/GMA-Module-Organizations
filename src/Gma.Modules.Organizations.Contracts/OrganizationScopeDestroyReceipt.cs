namespace Gma.Modules.Organizations.Contracts;

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
