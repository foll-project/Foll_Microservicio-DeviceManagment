using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using foll_device_managment.DeviceManagment.Application.Ports;
using foll_device_managment.DeviceManagment.Application.OutboundServices;
using foll_device_managment.Shared.Infrastructure.Messaging;

namespace foll_device_managment.DeviceManagment.Infrastructure.Messaging;

public record DeviceBrokerEvent(Guid EventId, Guid OperationId, DateTime OccurredAtUtc,
    string EventType, JsonElement Payload, long? OutboxMessageId = null, int Version = 1);

public sealed class RabbitMqDeviceNotificationPublisher(RabbitMqConnection connection) : IDeviceNotificationGateway
{
    public const string TelemetryType = "device-managment.telemetry.v1";
    public static DeviceBrokerEvent CreateMessage(DeviceEventEnvelope value)
    {
        if (value.OutboxMessageId <= 0 || value.EventId != $"device-managment:{value.OutboxMessageId}" ||
            !DeviceEventDelivery.SupportedTypes.Contains(value.Type)) throw new InvalidDataException("Evento de Devices invalido.");
        var occurred = value.Payload.TryGetProperty("ReportedAtUtc", out var reported) ? reported.GetDateTime() : value.Payload.GetProperty("DetectedAtUtc").GetDateTime();
        if (occurred.Kind != DateTimeKind.Utc) throw new InvalidDataException("Fecha de evento invalida.");
        var id = StableId(value.EventId);
        return new(id, id, occurred, value.Type, value.Payload.Clone(), value.OutboxMessageId);
    }
    public Task DeliverEventAsync(DeviceEventEnvelope message, CancellationToken ct) => Publish(CreateMessage(message), ct);
    public Task DeliverTelemetryAsync(DeviceTelemetrySnapshot snapshot, CancellationToken ct)
    {
        var id = StableId($"telemetry:{snapshot.DeviceId}:{snapshot.LastHeartbeatAtUtc:O}");
        return Publish(new(id, id, snapshot.LastHeartbeatAtUtc, TelemetryType, JsonSerializer.SerializeToElement(snapshot)), ct);
    }
    private Task Publish(DeviceBrokerEvent value, CancellationToken ct) => connection.PublishAsync(value.EventType,
        value.EventId, value.OperationId, value.OccurredAtUtc, JsonSerializer.SerializeToUtf8Bytes(value), ct);
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
