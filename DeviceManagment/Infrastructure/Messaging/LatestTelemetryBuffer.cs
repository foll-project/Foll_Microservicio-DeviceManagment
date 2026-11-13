using foll_device_managment.DeviceManagment.Application.OutboundServices;
using foll_device_managment.DeviceManagment.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
namespace foll_device_managment.DeviceManagment.Infrastructure.Messaging;
public class LatestTelemetryBuffer(IOptions<TelemetryOptions> options) : IDeviceTelemetryRealtimePublisher
{
    private readonly object _gate = new();
    private readonly Dictionary<long, DeviceTelemetrySnapshot> _pending = new();
    public long Dropped { get; private set; }
    public Task PublishTelemetryAsync(DeviceTelemetrySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(snapshot.DeviceId, out var previous))
            {
                if (snapshot.LastHeartbeatAtUtc > previous.LastHeartbeatAtUtc) _pending[snapshot.DeviceId] = snapshot;
            }
            else if (_pending.Count < options.Value.Capacity) _pending[snapshot.DeviceId] = snapshot;
            else Dropped++;
        }
        return Task.CompletedTask;
    }
    public IReadOnlyList<DeviceTelemetrySnapshot> TakeBatch(int count, DateTime now)
    {
        lock (_gate)
        {
            var entries = _pending.Values.Take(count).ToArray();
            foreach (var entry in entries) _pending.Remove(entry.DeviceId);
            return entries.Where(s => s.LastHeartbeatAtUtc >= now.AddSeconds(-options.Value.MaxAgeSeconds)).ToArray();
        }
    }
}
