namespace Gma.Modules.Organizations.Tests.Domain;

using Gma.Modules.Organizations.Domain.Entities;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationScopeLifecycleStateTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Scope_state_advances_and_closes_with_exact_replay_identity()
    {
        Guid organizationId = Id(1);
        Guid operationId = Id(2);
        string requestSha256 = new('a', 64);
        OrganizationScopeState state =
            OrganizationScopeState.Create(organizationId).Value;

        Assert.Equal(organizationId.ToString("D"), state.ScopeId);
        Assert.True(state.RegisterMutation());
        Assert.Equal(1, state.Version);
        Assert.Equal(
            OrganizationScopeCloseTransition.Completed,
            state.Close(operationId, requestSha256, Now));
        Assert.Equal(2, state.Version);
        Assert.False(state.RegisterMutation());
        Assert.Equal(
            OrganizationScopeCloseTransition.Replayed,
            state.Close(operationId, requestSha256, Now));
        Assert.Equal(
            OrganizationScopeCloseTransition.Conflict,
            state.Close(Id(3), requestSha256, Now));
    }

    [Fact]
    public void Destruction_progress_rolls_proof_and_creates_payload_free_receipt()
    {
        OrganizationScopeDestroyOperation operation =
            OrganizationScopeDestroyOperation.Create(
                Id(10),
                Id(11),
                new string('b', 64),
                expectedRevision: 3,
                resultingRevision: 4,
                batchSize: 2,
                maximumBatchSize: 100,
                Now).Value;
        string initialProof = operation.RemovalProofSha256;

        Assert.True(operation.AdvanceEmptyStage(Now));
        Assert.Equal(
            OrganizationScopeDestroyStage.OutboxMessages,
            operation.Stage);
        Assert.True(operation.RecordBatch(
            OrganizationScopeDestroyStage.OutboxMessages,
            removedRecordCount: 2,
            new string('c', 64),
            stageCompleted: true,
            Now));
        Assert.NotEqual(initialProof, operation.RemovalProofSha256);
        while (!operation.IsComplete)
        {
            Assert.True(operation.AdvanceEmptyStage(Now));
        }

        OrganizationScopeDestroyReceipt receipt =
            OrganizationScopeDestroyReceipt.Create(operation, Now).Value;

        Assert.Equal(2, receipt.RemovedRecordCount);
        Assert.Equal(1, receipt.CompletedBatchCount);
        Assert.Equal(operation.RemovalProofSha256, receipt.RemovalProofSha256);
        Assert.True(receipt.Matches(Id(11), new string('b', 64)));
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}
