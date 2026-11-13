namespace foll_device_managment.DeviceManagment.Domain.Model.Commands;

public record LinkDeviceCommand(long ActorUserId, long DeviceId, long PatientId);
