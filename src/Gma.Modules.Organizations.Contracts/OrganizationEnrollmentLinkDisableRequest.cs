namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentLinkDisableRequest(
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    long ExpectedVersion,
    string SubjectId,
    string ActorId);
