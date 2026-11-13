using foll_device_managment.DeviceManagment.Application.Ports;
using foll_device_managment.DeviceManagment.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
namespace foll_device_managment.DeviceManagment.Infrastructure.Messaging;
public class TelemetryPublisherBackgroundService(LatestTelemetryBuffer buffer, IServiceScopeFactory scopes,
    IOptions<TelemetryOptions> options, ILogger<TelemetryPublisherBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = scopes.CreateScope();
            var gateway = scope.ServiceProvider.GetRequiredService<IDeviceNotificationGateway>();
            foreach (var snapshot in buffer.TakeBatch(100, DateTime.UtcNow))
            {
                if (snapshot.LastHeartbeatAtUtc < DateTime.UtcNow.AddSeconds(-options.Value.MaxAgeSeconds)) continue;
                try { await gateway.DeliverTelemetryAsync(snapshot, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning(ex, "Snapshot efímero de {DeviceId} descartado; se renovará en otro heartbeat.", snapshot.DeviceId); }
            }
            try { await Task.Delay(options.Value.PollIntervalMilliseconds, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }
}
