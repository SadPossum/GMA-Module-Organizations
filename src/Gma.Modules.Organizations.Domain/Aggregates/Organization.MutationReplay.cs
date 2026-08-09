namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;

public sealed partial class Organization
{
    public bool HasLastMutationOperation(Guid operationId) =>
        operationId != Guid.Empty &&
        this.LastMutationOperationId == operationId;

    public bool IsExactProfileMutationReplay(
        Guid operationId,
        long expectedVersion,
        string actorId,
        string normalizedName,
        string normalizedSlug) =>
        this.IsExactMutationReplay(
            operationId,
            OrganizationChangeKind.ProfileUpdated,
            expectedVersion,
            actorId) &&
        string.Equals(this.Name, normalizedName, StringComparison.Ordinal) &&
        string.Equals(this.Slug, normalizedSlug, StringComparison.Ordinal);

    public bool IsExactLifecycleMutationReplay(
        Guid operationId,
        OrganizationChangeKind mutationKind,
        OrganizationState resultingState,
        long expectedVersion,
        string actorId) =>
        this.IsExactMutationReplay(
            operationId,
            mutationKind,
            expectedVersion,
            actorId) &&
        this.Status == resultingState;

    public bool IsExactOwnerCountMutationReplay(
        Guid operationId,
        long expectedVersion,
        string actorId,
        DateTimeOffset changedAtUtc) =>
        this.IsExactMutationReplay(
            operationId,
            OrganizationChangeKind.OwnerCountChanged,
            expectedVersion,
            actorId) &&
        this.LastChangedAtUtc == changedAtUtc;

    private bool IsExactMutationReplay(
        Guid operationId,
        OrganizationChangeKind mutationKind,
        long expectedVersion,
        string actorId) =>
        operationId != Guid.Empty &&
        mutationKind != OrganizationChangeKind.Unknown &&
        expectedVersion >= 0 &&
        expectedVersion < long.MaxValue &&
        this.Version == expectedVersion + 1 &&
        this.LastMutationOperationId == operationId &&
        this.LastMutationKind == mutationKind &&
        string.Equals(
            this.LastChangedBy,
            actorId.Trim(),
            StringComparison.Ordinal);

    private static Result ValidateMutationOperation(Guid operationId) =>
        operationId == Guid.Empty
            ? Result.Failure(OrganizationDomainErrors.MutationOperationRequired)
            : Result.Success();
}
