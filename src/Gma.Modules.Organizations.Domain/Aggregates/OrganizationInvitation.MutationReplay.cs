namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;

public sealed partial class OrganizationInvitation
{
    public bool IsExactRevocationReplay(long expectedVersion, string actorId)
    {
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        return actor.IsSuccess &&
            expectedVersion is > 0 and < long.MaxValue &&
            this.Status == OrganizationInvitationState.Revoked &&
            this.Version == expectedVersion + 1 &&
            string.Equals(
                this.LastChangedBy,
                actor.Value.Value,
                StringComparison.Ordinal);
    }
}
