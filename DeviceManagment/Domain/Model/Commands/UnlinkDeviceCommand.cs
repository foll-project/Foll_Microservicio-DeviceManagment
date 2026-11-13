namespace foll_device_managment.DeviceManagment.Domain.Model.Commands;

public record UnlinkDeviceCommand(long ActorUserId, long DeviceId);
