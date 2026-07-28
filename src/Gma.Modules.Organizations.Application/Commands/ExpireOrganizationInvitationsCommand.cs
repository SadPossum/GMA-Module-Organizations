namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record ExpireOrganizationInvitationsCommand(int BatchSize)
    : ITransactionalCommand<int>;
