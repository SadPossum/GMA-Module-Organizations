namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationMutationAdmissionContext(
    OrganizationMutationAdmissionOperation Operation,
    Guid OrganizationId,
    string ActorSubjectId,
    Guid? TargetId = null,
    string? TargetSubjectId = null);
