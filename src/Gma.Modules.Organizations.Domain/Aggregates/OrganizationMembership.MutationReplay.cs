namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;

public sealed partial class OrganizationMembership
{
    public bool HasLastMutationOperation(Guid operationId) =>
        operationId != Guid.Empty &&
        this.LastMutationOperationId == operationId;

    public bool IsExactMutationReplay(
        Guid operationId,
        OrganizationMembershipChangeKind mutationKind,
        OrganizationMembershipState resultingState,
        long expectedVersion,
        string actorId) =>
        operationId != Guid.Empty &&
        mutationKind != OrganizationMembershipChangeKind.Unknown &&
        expectedVersion >= 0 &&
        expectedVersion < long.MaxValue &&
        this.Version == expectedVersion + 1 &&
        this.LastMutationOperationId == operationId &&
        this.LastMutationKind == mutationKind &&
        this.Status == resultingState &&
        string.Equals(
            this.LastChangedBy,
            actorId.Trim(),
            StringComparison.Ordinal);

    private static Result ValidateMutationOperation(Guid? operationId) =>
        operationId == Guid.Empty
            ? Result.Failure(OrganizationDomainErrors.MutationOperationRequired)
            : Result.Success();
}
