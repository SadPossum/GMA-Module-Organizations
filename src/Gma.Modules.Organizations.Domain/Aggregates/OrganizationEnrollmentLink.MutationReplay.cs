namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;

public sealed partial class OrganizationEnrollmentLink
{
    public bool IsExactDisableReplay(long expectedVersion, string actorId)
    {
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        return actor.IsSuccess &&
            expectedVersion is > 0 and < long.MaxValue &&
            this.Status == OrganizationEnrollmentLinkState.Disabled &&
            this.Version == expectedVersion + 1 &&
            string.Equals(
                this.LastChangedBy,
                actor.Value.Value,
                StringComparison.Ordinal);
    }
}
