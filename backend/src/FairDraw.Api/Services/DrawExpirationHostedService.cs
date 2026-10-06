using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FairDraw.Api.Services;

/// <summary>
/// Periodically runs DrawExpirationPurger. Deliberately not registered under the "Testing"
/// environment (see Program.cs) so integration tests stay deterministic — the purge logic itself
/// is unit-tested directly against DrawExpirationPurger instead of through this timer loop.
/// </summary>
public class DrawExpirationHostedService(DrawExpirationPurger purger, ILogger<DrawExpirationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await purger.PurgeExpiredDrawsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Draw expiration sweep failed; will retry on the next interval.");
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
