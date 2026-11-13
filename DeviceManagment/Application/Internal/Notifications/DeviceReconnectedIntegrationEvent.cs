using foll_device_managment.DeviceManagment.Domain.Model.Enums;
using MediatR;

namespace foll_device_managment.DeviceManagment.Application.Internal.Notifications;

public record DeviceReconnectedIntegrationEvent(
    long DeviceId,
    long PatientId,
    DateTime ReportedAtUtc,
    ConnectivityChangeCause Cause) : INotification;
