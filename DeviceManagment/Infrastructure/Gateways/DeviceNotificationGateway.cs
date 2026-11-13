using foll_device_managment.DeviceManagment.Application.Ports;
using foll_device_managment.DeviceManagment.Application.OutboundServices;

namespace foll_device_managment.DeviceManagment.Infrastructure.Gateways;

public class DeviceNotificationGateway(HttpClient client) : IDeviceNotificationGateway
{
    public async Task DeliverEventAsync(DeviceEventEnvelope message, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("internal/notifications/device-events", message, ct);
        response.EnsureSuccessStatusCode();
    }
    public async Task DeliverTelemetryAsync(DeviceTelemetrySnapshot snapshot, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("internal/notifications/device-telemetry", snapshot, ct);
        response.EnsureSuccessStatusCode();
    }
}
