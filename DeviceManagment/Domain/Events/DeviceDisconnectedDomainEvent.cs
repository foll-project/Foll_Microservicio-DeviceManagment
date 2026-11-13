using foll_device_managment.DeviceManagment.Domain.Model.Enums;
using foll_device_managment.Shared.Domain.Events;

namespace foll_device_managment.DeviceManagment.Domain.Events;

public record DeviceDisconnectedDomainEvent(
    long DeviceId,
    long AssignedPatientId,
    DateTime DetectedAtUtc,
    DateTime LastSeenAtUtc,
    ConnectivityChangeCause Cause) : IDomainEvent
{
    public DateTime OccurredOn => DetectedAtUtc;
}
