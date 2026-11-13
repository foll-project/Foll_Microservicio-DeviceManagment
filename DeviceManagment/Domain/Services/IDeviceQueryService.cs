using DeviceEntity = foll_device_managment.DeviceManagment.Domain.Model.Entities.Device;
using foll_device_managment.DeviceManagment.Domain.Model.Queries;

namespace foll_device_managment.DeviceManagment.Domain.Services;

public interface IDeviceQueryService
{
    Task<DeviceEntity?> Handle(GetDeviceStatusByIdQuery query);
    Task<DeviceEntity?> Handle(GetDeviceByPatientIdQuery query);
}
