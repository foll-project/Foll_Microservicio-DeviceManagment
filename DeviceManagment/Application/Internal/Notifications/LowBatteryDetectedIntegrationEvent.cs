using MediatR;

namespace foll_device_managment.DeviceManagment.Application.Internal.Notifications;

public record LowBatteryDetectedIntegrationEvent(
    long DeviceId,
    long PatientId,
    short BatteryLevel,
    DateTime ReportedAtUtc) : INotification;
