namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Ports;

internal sealed class DefaultOrganizationInvitationAdmissionPolicy : IOrganizationInvitationAdmissionPolicy
{
    public Task<Result> CanAcceptInvitationAsync(
        string subjectId,
        string? recipientEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(recipientEmail is null
            ? Result.Success()
            : Result.Failure(OrganizationApplicationErrors.RecipientVerificationRequired));
    }
}
