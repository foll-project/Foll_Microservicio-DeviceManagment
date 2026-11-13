namespace foll_device_managment.DeviceManagment.Application.OutboundServices;

public interface IPatientAccessService
{
    Task<bool> CanManageDevicesAsync(long actorUserId, long patientId);
}
