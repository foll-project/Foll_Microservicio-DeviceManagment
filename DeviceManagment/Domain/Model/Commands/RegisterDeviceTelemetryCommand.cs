namespace foll_device_managment.DeviceManagment.Domain.Model.Commands;

public record RegisterDeviceTelemetryCommand(
    long DeviceId,
    short BatteryLevel,
    bool IsCharging,
    DateTime ReportedAtUtc,
    string? FirmwareVersion = null);
