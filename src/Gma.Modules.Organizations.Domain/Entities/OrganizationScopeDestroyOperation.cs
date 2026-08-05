namespace Gma.Modules.Organizations.Domain.Entities;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

public sealed class OrganizationScopeDestroyOperation
{
    public const int RemovalProofVersion = 1;
    public static readonly string InitialRemovalProofSha256 = Sha256(
        "gma-organization-scope-destroy-proof/v1|empty");

    private OrganizationScopeDestroyOperation() { }

    private OrganizationScopeDestroyOperation(
        Guid organizationId,
        Guid operationId,
        string requestSha256,
        long expectedRevision,
        long resultingRevision,
        int batchSize,
        DateTimeOffset startedAtUtc)
    {
        this.OrganizationId = organizationId;
        this.OperationId = operationId;
        this.RequestSha256 = requestSha256;
        this.ExpectedRevision = expectedRevision;
        this.ResultingRevision = resultingRevision;
        this.BatchSize = batchSize;
        this.Stage = OrganizationScopeDestroyStage.InboxMessages;
        this.RemovalProofSha256 = InitialRemovalProofSha256;
        this.StartedAtUtc = startedAtUtc;
        this.UpdatedAtUtc = startedAtUtc;
    }

    public Guid OrganizationId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestSha256 { get; private set; } = string.Empty;
    public long ExpectedRevision { get; private set; }
    public long ResultingRevision { get; private set; }
    public int BatchSize { get; private set; }
    public OrganizationScopeDestroyStage Stage { get; private set; }
    public long RemovedRecordCount { get; private set; }
    public int CompletedBatchCount { get; private set; }
    public int ProofVersion { get; private set; } = RemovalProofVersion;
    public string RemovalProofSha256 { get; private set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsComplete => this.Stage == OrganizationScopeDestroyStage.Completed;

    public static Result<OrganizationScopeDestroyOperation> Create(
        Guid organizationId,
        Guid operationId,
        string requestSha256,
        long expectedRevision,
        long resultingRevision,
        int batchSize,
        int maximumBatchSize,
        DateTimeOffset startedAtUtc)
    {
        if (organizationId == Guid.Empty ||
            operationId == Guid.Empty ||
            !IsSha256(requestSha256) ||
            expectedRevision < 0 ||
            expectedRevision == long.MaxValue ||
            resultingRevision <= expectedRevision ||
            batchSize is < 1 ||
            batchSize > maximumBatchSize ||
            startedAtUtc == default)
        {
            return Result.Failure<OrganizationScopeDestroyOperation>(
                OrganizationDomainErrors.ScopeDestroyOperationInvalid);
        }

        return Result.Success(new OrganizationScopeDestroyOperation(
            organizationId,
            operationId,
            requestSha256,
            expectedRevision,
            resultingRevision,
            batchSize,
            startedAtUtc));
    }

    public bool Matches(Guid operationId, string requestSha256) =>
        this.OperationId == operationId &&
        string.Equals(
            this.RequestSha256,
            requestSha256,
            StringComparison.Ordinal);

    public bool RecordBatch(
        OrganizationScopeDestroyStage stage,
        int removedRecordCount,
        string removedRecordIdsSha256,
        bool stageCompleted,
        DateTimeOffset recordedAtUtc)
    {
        if (this.IsComplete ||
            stage != this.Stage ||
            removedRecordCount is < 1 ||
            removedRecordCount > this.BatchSize ||
            !IsSha256(removedRecordIdsSha256) ||
            recordedAtUtc < this.UpdatedAtUtc ||
            this.RemovedRecordCount > long.MaxValue - removedRecordCount ||
            this.CompletedBatchCount == int.MaxValue)
        {
            return false;
        }

        int nextBatch = this.CompletedBatchCount + 1;
        this.RemovalProofSha256 = Sha256(
            "gma-organization-scope-destroy-proof/v1|" +
            $"{this.RemovalProofSha256}|{nextBatch}|{(int)stage}|" +
            $"{removedRecordCount}|{removedRecordIdsSha256}");
        this.RemovedRecordCount += removedRecordCount;
        this.CompletedBatchCount = nextBatch;
        this.UpdatedAtUtc = recordedAtUtc;
        if (stageCompleted)
        {
            this.Stage = Next(stage);
        }

        return true;
    }

    public bool AdvanceEmptyStage(DateTimeOffset recordedAtUtc)
    {
        if (this.IsComplete || recordedAtUtc < this.UpdatedAtUtc)
        {
            return false;
        }

        this.Stage = Next(this.Stage);
        this.UpdatedAtUtc = recordedAtUtc;
        return true;
    }

    private static OrganizationScopeDestroyStage Next(
        OrganizationScopeDestroyStage stage) =>
        stage switch
        {
            OrganizationScopeDestroyStage.InboxMessages =>
                OrganizationScopeDestroyStage.OutboxMessages,
            OrganizationScopeDestroyStage.OutboxMessages =>
                OrganizationScopeDestroyStage.EnrollmentClaims,
            OrganizationScopeDestroyStage.EnrollmentClaims =>
                OrganizationScopeDestroyStage.Invitations,
            OrganizationScopeDestroyStage.Invitations =>
                OrganizationScopeDestroyStage.EnrollmentLinks,
            OrganizationScopeDestroyStage.EnrollmentLinks =>
                OrganizationScopeDestroyStage.Memberships,
            OrganizationScopeDestroyStage.Memberships =>
                OrganizationScopeDestroyStage.Organization,
            OrganizationScopeDestroyStage.Organization =>
                OrganizationScopeDestroyStage.Completed,
            _ => throw new InvalidOperationException(
                "The organization scope destruction stage is invalid.")
        };

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public enum OrganizationScopeDestroyStage
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
