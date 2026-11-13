namespace foll_device_managment.DeviceManagment.Domain.Model.Commands;

public record CheckDeviceConnectivityCommand(long DeviceId, DateTime DetectedAtUtc, TimeSpan DisconnectThreshold);
