namespace foll_device_managment.DeviceManagment.Application.ACL;

public interface IDeviceStatusAcl
{
    Task<DeviceStatusDto?> GetStatusByPatientIdAsync(long patientId);
}
