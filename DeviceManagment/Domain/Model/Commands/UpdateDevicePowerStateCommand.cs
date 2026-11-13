namespace foll_device_managment.DeviceManagment.Domain.Model.Commands;

public record UpdateDevicePowerStateCommand(long DeviceId, bool IsActive, DateTime ReportedAtUtc);
