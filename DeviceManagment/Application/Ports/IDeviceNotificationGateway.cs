using System.Text.Json;
using foll_device_managment.DeviceManagment.Application.OutboundServices;

namespace foll_device_managment.DeviceManagment.Application.Ports;

public record DeviceEventEnvelope(string EventId, long OutboxMessageId, string Type, JsonElement Payload);
public interface IDeviceNotificationGateway
{
    Task DeliverEventAsync(DeviceEventEnvelope message, CancellationToken ct);
    Task DeliverTelemetryAsync(DeviceTelemetrySnapshot snapshot, CancellationToken ct);
}
