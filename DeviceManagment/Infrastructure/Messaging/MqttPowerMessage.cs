using System.Text.Json.Serialization;

namespace foll_device_managment.DeviceManagment.Infrastructure.Messaging;

public class MqttPowerMessage
{
    [JsonPropertyName("device_id")]
    public long DeviceId { get; init; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime? ReportedAtUtc { get; init; }
}
