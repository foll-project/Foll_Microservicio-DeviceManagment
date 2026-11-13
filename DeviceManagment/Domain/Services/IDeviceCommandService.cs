using foll_device_managment.DeviceManagment.Domain.Model.Commands;

namespace foll_device_managment.DeviceManagment.Domain.Services;

public interface IDeviceCommandService
{
    Task Handle(LinkDeviceCommand command);
    Task Handle(UnlinkDeviceCommand command);
    Task Handle(RegisterDeviceTelemetryCommand command);
    Task Handle(CheckDeviceConnectivityCommand command);
    Task Handle(UpdateDevicePowerStateCommand command);
}
