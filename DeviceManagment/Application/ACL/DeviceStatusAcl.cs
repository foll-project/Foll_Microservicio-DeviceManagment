using ConnectivityStatusEnum = foll_device_managment.DeviceManagment.Domain.Model.Enums.ConnectivityStatus;
using foll_device_managment.DeviceManagment.Domain.Repositories;
using foll_device_managment.DeviceManagment.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace foll_device_managment.DeviceManagment.Application.ACL;

public class DeviceStatusAcl : IDeviceStatusAcl
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly DeviceMonitoringOptions _monitoringOptions;

    public DeviceStatusAcl(IDeviceRepository deviceRepository, IOptions<DeviceMonitoringOptions> monitoringOptions)
    {
        _deviceRepository = deviceRepository;
        _monitoringOptions = monitoringOptions.Value;
    }

    public async Task<DeviceStatusDto?> GetStatusByPatientIdAsync(long patientId)
    {
        if (patientId <= 0) return null;

        var device = await _deviceRepository.FindByAssignedPatientIdAsync(patientId);
        if (device is null) return null;

        var isOnline = device.ConnectivityStatus == ConnectivityStatusEnum.Connected;

        var isLowBattery = device.CurrentBatteryLevel.HasValue &&
                           device.CurrentBatteryLevel.Value < _monitoringOptions.LowBatteryThreshold &&
                           device.IsCharging == false;

        return new DeviceStatusDto(
            device.DeviceId,
            device.AssignedPatientId,
            device.Status.ToString(),
            device.ConnectivityStatus?.ToString(),
            device.CurrentBatteryLevel,
            device.IsCharging,
            device.LastHeartbeatAt,
            device.MonitoringStartedAt,
            device.LastConnectivityChangeAt,
            isOnline,
            isLowBattery,
            device.FirmwareVersion);
    }
}
