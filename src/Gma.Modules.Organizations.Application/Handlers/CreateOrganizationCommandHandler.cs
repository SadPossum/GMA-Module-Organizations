namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Contracts;

internal sealed class CreateOrganizationCommandHandler(
    OrganizationCreationWorkflow creation,
    OrganizationCreationAdmissionPolicy admissionPolicy)
    : ICommandHandler<CreateOrganizationCommand, OrganizationMembershipSummaryDto>
{
    public async Task<Result<OrganizationMembershipSummaryDto>> HandleAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        Result<OrganizationCreationWorkflowResult> result =
            await creation.ExecuteAsync(
                command.OperationId,
                command.Name,
                command.Slug,
                command.SubjectId,
                command.ActorId,
                normalized => OrganizationCreationFingerprint.Compute(
                    normalized.Name,
                    normalized.Slug,
                    normalized.SubjectId,
                    normalized.ActorId),
                (normalized, token) => admissionPolicy.AuthorizeAsync(
                    new OrganizationCreationAdmissionRequest(
                        normalized.OperationId,
                        normalized.Name,
                        normalized.Slug,
                        normalized.SubjectId,
                        normalized.ActorId),
                    token),
                OrganizationCreationReplayMembership.Active,
                cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result.Success(result.Value.Summary)
            : Result.Failure<OrganizationMembershipSummaryDto>(result.Error);
    }
}
