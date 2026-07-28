namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Maintenance;
using Gma.Modules.Organizations.Application.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class OrganizationsLifecycleService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrganizationsLifecycleOptions> options,
    ILogger<OrganizationsLifecycleService> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogIterationFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(LogIterationFailed)),
            "Organizations lifecycle iteration failed with {ExceptionType}; processing will retry.");

    private static readonly Action<ILogger, int, int, int, Exception?> LogTransitionsCompleted =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogTransitionsCompleted)),
            "Organizations lifecycle expired {InvitationCount} invitations, {ClaimCount} claims, and {LinkCount} enrollment links.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(options.Value.IntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.ProcessAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogIterationFailed(logger, exception.GetType().Name, null);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal async Task ProcessAsync(CancellationToken cancellationToken)
    {
        OrganizationsLifecycleOptions settings = options.Value;
        int claimCount = await BoundedBatchProcessor.ExecuteAsync(
                settings.BatchSize,
                settings.MaxBatchesPerCategoryPerCycle,
                (batchSize, token) => this.DispatchAsync(
                    new ExpireOrganizationEnrollmentClaimsCommand(batchSize), token),
                cancellationToken)
            .ConfigureAwait(false);
        int invitationCount = await BoundedBatchProcessor.ExecuteAsync(
                settings.BatchSize,
                settings.MaxBatchesPerCategoryPerCycle,
                (batchSize, token) => this.DispatchAsync(
                    new ExpireOrganizationInvitationsCommand(batchSize), token),
                cancellationToken)
            .ConfigureAwait(false);
        int linkCount = await BoundedBatchProcessor.ExecuteAsync(
                settings.BatchSize,
                settings.MaxBatchesPerCategoryPerCycle,
                (batchSize, token) => this.DispatchAsync(
                    new ExpireOrganizationEnrollmentLinksCommand(batchSize), token),
                cancellationToken)
            .ConfigureAwait(false);

        if (invitationCount > 0 || claimCount > 0 || linkCount > 0)
        {
            LogTransitionsCompleted(logger, invitationCount, claimCount, linkCount, null);
        }
    }

    private async Task<int> DispatchAsync(ICommand<int> command, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        Result<int> result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                $"Organizations lifecycle command failed with code '{result.Error.Code}'.");
    }
}
