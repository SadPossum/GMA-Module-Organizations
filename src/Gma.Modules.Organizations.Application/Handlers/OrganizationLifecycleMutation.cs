namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;

internal readonly record struct OrganizationLifecycleMutation(
    OrganizationLifecycleAction Action,
    OrganizationMutationAdmissionOperation AdmissionOperation,
    OrganizationChangeKind ChangeKind,
    OrganizationState ResultingState)
{
    public static bool TryResolve(
        OrganizationLifecycleAction action,
        out OrganizationLifecycleMutation mutation)
    {
        mutation = action switch
        {
            OrganizationLifecycleAction.Suspend => new(
                action,
                OrganizationMutationAdmissionOperation.SuspendOrganization,
                OrganizationChangeKind.Suspended,
                OrganizationState.Suspended),
            OrganizationLifecycleAction.Reactivate => new(
                action,
                OrganizationMutationAdmissionOperation.ReactivateOrganization,
                OrganizationChangeKind.Reactivated,
                OrganizationState.Active),
            OrganizationLifecycleAction.Archive => new(
                action,
                OrganizationMutationAdmissionOperation.ArchiveOrganization,
                OrganizationChangeKind.Archived,
                OrganizationState.Archived),
            _ => default
        };
        return mutation.Action != OrganizationLifecycleAction.Unknown;
    }

    public bool IsExactReplay(
        Organization organization,
        Guid operationId,
        long expectedVersion,
        string actorId) =>
        organization.IsExactLifecycleMutationReplay(
            operationId,
            this.ChangeKind,
            this.ResultingState,
            expectedVersion,
            actorId);

    public Result Apply(
        Organization organization,
        long expectedVersion,
        string actorId,
        Guid operationId,
        Guid eventId,
        DateTimeOffset nowUtc) =>
        this.Action switch
        {
            OrganizationLifecycleAction.Suspend => organization.Suspend(
                expectedVersion,
                actorId,
                operationId,
                eventId,
                nowUtc),
            OrganizationLifecycleAction.Reactivate => organization.Reactivate(
                expectedVersion,
                actorId,
                operationId,
                eventId,
                nowUtc),
            OrganizationLifecycleAction.Archive => organization.Archive(
                expectedVersion,
                actorId,
                operationId,
                eventId,
                nowUtc),
            _ => Result.Failure(
                OrganizationApplicationErrors.OrganizationLifecycleActionInvalid)
        };
}
