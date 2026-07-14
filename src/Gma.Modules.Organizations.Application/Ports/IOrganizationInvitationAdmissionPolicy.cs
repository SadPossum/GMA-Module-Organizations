namespace Gma.Modules.Organizations.Application.Ports;

using Gma.Framework.Results;

public interface IOrganizationInvitationAdmissionPolicy
{
    Task<Result> CanAcceptInvitationAsync(
        string subjectId,
        string? recipientEmail,
        CancellationToken cancellationToken);
}
