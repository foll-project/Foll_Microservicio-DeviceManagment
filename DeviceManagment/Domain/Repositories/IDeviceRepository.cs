using DeviceEntity = foll_device_managment.DeviceManagment.Domain.Model.Entities.Device;
using foll_device_managment.Shared.Domain.Repositories;

namespace foll_device_managment.DeviceManagment.Domain.Repositories;

public interface IDeviceRepository : IBaseRepository<DeviceEntity>
{
    Task<DeviceEntity?> FindByAssignedPatientIdAsync(long patientId);
    Task<IEnumerable<DeviceEntity>> FindByPatientIdsAsync(IEnumerable<long> patientIds);
    Task<IReadOnlyCollection<long>> ListMonitoredActiveDeviceIdsAsync();
}
