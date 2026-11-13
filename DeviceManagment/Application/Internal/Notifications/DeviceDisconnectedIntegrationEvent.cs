using foll_device_managment.DeviceManagment.Domain.Model.Enums;
using MediatR;

namespace foll_device_managment.DeviceManagment.Application.Internal.Notifications;

public record DeviceDisconnectedIntegrationEvent(
    long DeviceId,
    long PatientId,
    DateTime DetectedAtUtc,
    DateTime LastSeenAtUtc,
    ConnectivityChangeCause Cause) : INotification;
