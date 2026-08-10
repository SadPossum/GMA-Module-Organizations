namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationInvitationRecipientVerificationPolicy
{
    ValueTask<OrganizationInvitationRecipientVerificationDecision> EvaluateAsync(
        OrganizationInvitationRecipientVerificationRequest request,
        CancellationToken cancellationToken = default);
}
