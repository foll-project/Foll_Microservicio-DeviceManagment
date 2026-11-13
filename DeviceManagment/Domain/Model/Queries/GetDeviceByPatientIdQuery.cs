namespace foll_device_managment.DeviceManagment.Domain.Model.Queries;

public record GetDeviceByPatientIdQuery(long ActorUserId, long PatientId);
