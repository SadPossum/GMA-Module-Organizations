namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;

internal sealed class ProvisionOrganizationCommandHandler(
    OrganizationCreationWorkflow creation)
    : ICommandHandler<ProvisionOrganizationCommand, OrganizationProvisioningResult>
{
    public async Task<Result<OrganizationProvisioningResult>> HandleAsync(
        ProvisionOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        Result<OrganizationCreationWorkflowResult> result =
            await creation.ExecuteAsync(
                command.OrganizationId,
                command.Name,
                command.Slug,
                command.InitialOwnerSubjectId,
                command.ActorId,
                normalized => OrganizationCreationFingerprint.ComputeProvisioning(
                    normalized.Name,
                    normalized.Slug,
                    normalized.SubjectId),
                static (_, _) => ValueTask.FromResult(Result.Success()),
                OrganizationCreationReplayMembership.Existing,
                cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Result.Success(new OrganizationProvisioningResult(
                result.Value.WasCreated
                    ? OrganizationProvisioningOutcome.Provisioned
                    : OrganizationProvisioningOutcome.AlreadyProvisioned,
                result.Value.Summary,
                ErrorCode: null));
        }

        OrganizationProvisioningOutcome outcome = result.Error ==
            OrganizationApplicationErrors.CreationOperationConflict
            ? OrganizationProvisioningOutcome.IdentityConflict
            : result.Error == OrganizationApplicationErrors.SlugConflict
                ? OrganizationProvisioningOutcome.SlugConflict
                : OrganizationProvisioningOutcome.InvalidRequest;
        return Result.Success(new OrganizationProvisioningResult(
            outcome,
            Summary: null,
            result.Error.Code));
    }
}
