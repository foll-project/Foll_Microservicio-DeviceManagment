using foll_device_managment.DeviceManagment.Domain.Model.Enums;
using foll_device_managment.Shared.Domain.Events;

namespace foll_device_managment.DeviceManagment.Domain.Events;

public record DeviceReconnectedDomainEvent(
    long DeviceId,
    long AssignedPatientId,
    DateTime ReportedAtUtc,
    ConnectivityChangeCause Cause) : IDomainEvent
{
    public DateTime OccurredOn => ReportedAtUtc;
}
