using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using foll_device_managment.Shared.Infrastructure.Messaging;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_device_managment.DeviceManagment.Domain.Model.Entities;
static class Fixture
{
    static long sequence = 9000000, deviceId, otherDeviceId, patientId, otherPatientId;
    public static IBrokerMessageHandler Handler(AppDbContext db) => new AccountDeletionCompletedHandler(db);
    public static Task<BrokerEnvelope> SeedAsync(AppDbContext db)
    {
        deviceId = ++sequence; otherDeviceId = ++sequence;
        patientId = deviceId + 1000000; otherPatientId = otherDeviceId + 1000000;
        var device = new Device(deviceId, "checks");
        device.AssignToPatient(patientId, DateTime.UtcNow);
        var other = new Device(otherDeviceId, "checks");
        other.AssignToPatient(otherPatientId, DateTime.UtcNow);
        db.Devices.AddRange(device, other);
        var e = new AccountDeletionCompleted(Guid.NewGuid(), Guid.NewGuid(), 101, new long[] {patientId}, DateTime.UtcNow);
        return Task.FromResult(new BrokerEnvelope(e.EventId, e.OperationId, AccountDeletionCompleted.EventType, JsonSerializer.Serialize(e)));
    }
    public static async Task AssertAsync(AppDbContext db, Action<bool,string> check)
    {
        var device = await db.Devices.SingleAsync(d => d.DeviceId == deviceId);
        check(device.AssignedPatientId is null && device.MonitoringStartedAt is null && device.ConnectivityStatus is null,
            "deleted patient's device released and monitoring reset");
        check((await db.Devices.SingleAsync(d => d.DeviceId == otherDeviceId)).AssignedPatientId == otherPatientId, "unrelated patient's device untouched");
    }
}

