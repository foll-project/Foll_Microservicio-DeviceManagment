namespace foll_device_managment.DeviceManagment.Application.ACL;

public interface IDeviceAssignmentAcl
{
    Task<DeviceAssignmentDto?> GetAssignmentByDeviceIdAsync(long deviceId);
}
