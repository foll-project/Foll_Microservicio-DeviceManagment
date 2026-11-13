using foll_device_managment.Shared.Domain.Events;

namespace foll_device_managment.DeviceManagment.Domain.Events;

public record LowBatteryDetectedDomainEvent(
    long DeviceId,
    long AssignedPatientId,
    short BatteryLevel,
    DateTime ReportedAtUtc) : IDomainEvent
{
    public DateTime OccurredOn => ReportedAtUtc;
}
