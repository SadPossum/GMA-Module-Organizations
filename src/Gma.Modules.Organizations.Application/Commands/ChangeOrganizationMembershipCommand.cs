namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public enum OrganizationMembershipAction
{
    Unknown = 0,
    Suspend = 1,
    Resume = 2,
    Remove = 3
}

public sealed record ChangeOrganizationMembershipCommand(
    Guid OrganizationId,
    string TargetSubjectId,
    OrganizationMembershipAction Action,
    long ExpectedOrganizationVersion,
    long ExpectedMembershipVersion,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationMembershipDto>;
