namespace foll_device_managment.DeviceManagment.Application.ACL;

public record DeviceAssignmentDto(long DeviceId, long? AssignedPatientId, string Status, string? ConnectivityStatus);
