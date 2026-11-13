using foll_device_managment.DeviceManagment.Domain.Model.Entities;
using foll_device_managment.DeviceManagment.Domain.Model.Enums;
using foll_device_managment.Shared.Domain.Repositories;

namespace foll_device_managment.DeviceManagment.Domain.Repositories;

public interface IDeviceEventRepository : IBaseRepository<DeviceEvent>
{
    Task<DeviceEvent?> FindLatestUnresolvedByDeviceIdAndTypeAsync(long deviceId, DeviceEventType eventType);
    Task DeleteByDeviceIdAsync(long deviceId);
}
