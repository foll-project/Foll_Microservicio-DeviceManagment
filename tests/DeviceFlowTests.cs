using foll_device_managment.DeviceManagment.Application.Internal.CommandServices;
using foll_device_managment.DeviceManagment.Application.OutboundServices;
using foll_device_managment.DeviceManagment.Application.Internal.Notifications;
using foll_device_managment.DeviceManagment.Domain.Model.Commands;
using foll_device_managment.DeviceManagment.Domain.Model.Entities;
using foll_device_managment.DeviceManagment.Domain.Model.Enums;
using foll_device_managment.DeviceManagment.Domain.Repositories;
using foll_device_managment.DeviceManagment.Infrastructure.Configuration;
using foll_device_managment.Shared.Domain.Model.Entities;
using foll_device_managment.Shared.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
namespace Foll.DeviceManagment.Tests;
public class DeviceFlowTests
{
    [Fact]
    public async Task LowBatteryAlertsOnlyOnTransitionAndRecoveryIsDurable()
    {
        var f = new Fixture(); await f.Service.Handle(new LinkDeviceCommand(7, 1001, 25));
        var now = DateTime.UtcNow;
        await f.Service.Handle(new RegisterDeviceTelemetryCommand(1001, 10, false, now, null));
        await f.Service.Handle(new RegisterDeviceTelemetryCommand(1001, 9, false, now.AddSeconds(10), null));
        Assert.Single(f.Outbox.Items); Assert.Single(f.Events.Items);
        await f.Service.Handle(new RegisterDeviceTelemetryCommand(1001, 20, false, now.AddSeconds(20), null));
        Assert.Equal(2, f.Outbox.Items.Count);
        Assert.True(f.Events.Items[0].IsResolved);
        Assert.Equal(DeviceManagmentEventTypes.LowBatteryResolvedV1, f.Outbox.Items[1].Type);
        Assert.Equal(3, f.Telemetry.Snapshots.Count);
    }
    [Fact]
    public async Task DisconnectAndHeartbeatRecoveryQueueTwoEvents()
    {
        var f = new Fixture(); await f.Service.Handle(new LinkDeviceCommand(7, 1001, 25));
        var time = f.Devices.Items[0].MonitoringStartedAt!.Value;
        await f.Service.Handle(new CheckDeviceConnectivityCommand(1001, time.AddSeconds(31), TimeSpan.FromSeconds(30)));
        await f.Service.Handle(new CheckDeviceConnectivityCommand(1001, time.AddSeconds(32), TimeSpan.FromSeconds(30)));
        Assert.Single(f.Outbox.Items);
        await f.Service.Handle(new RegisterDeviceTelemetryCommand(1001, 80, false, time.AddSeconds(35), null));
        Assert.Equal(ConnectivityStatus.Connected, f.Devices.Items[0].ConnectivityStatus);
        Assert.True(f.Events.Items[0].IsResolved);
        Assert.Equal(DeviceManagmentEventTypes.DeviceReconnectedV1, f.Outbox.Items[1].Type);
    }
    [Fact]
    public async Task UnauthorizedLinkDoesNotAssignOrCommit()
    {
        var f = new Fixture(); f.Access.Allowed = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.Handle(new LinkDeviceCommand(99, 1001, 25)));
        Assert.Null(f.Devices.Items[0].AssignedPatientId); Assert.Equal(0, f.Commits.Count);
    }
    [Fact]
    public async Task PatientCannotHaveTwoDevices()
    {
        var f = new Fixture(); f.Devices.Items.Add(new Device(1002, "1.0"));
        await f.Service.Handle(new LinkDeviceCommand(7, 1001, 25));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.Handle(new LinkDeviceCommand(7, 1002, 25)));
        Assert.Null(f.Devices.Items[1].AssignedPatientId);
    }
    [Fact]
    public async Task UnlinkClearsMonitoringAndBattery()
    {
        var f = new Fixture(); await f.Service.Handle(new LinkDeviceCommand(7, 1001, 25));
        await f.Service.Handle(new RegisterDeviceTelemetryCommand(1001, 80, true, DateTime.UtcNow, null));
        await f.Service.Handle(new UnlinkDeviceCommand(7, 1001));
        var device = f.Devices.Items[0];
        Assert.Null(device.AssignedPatientId); Assert.Null(device.MonitoringStartedAt); Assert.Null(device.CurrentBatteryLevel);
    }
    [Fact]
    public async Task StaleHeartbeatDoesNotPublishOrCommit()
    {
        var f = new Fixture(); await f.Service.Handle(new LinkDeviceCommand(7, 1001, 25));
        var now = DateTime.UtcNow;
        await f.Service.Handle(new RegisterDeviceTelemetryCommand(1001, 80, false, now, null));
        var count = f.Commits.Count;
        await f.Service.Handle(new RegisterDeviceTelemetryCommand(1001, 5, false, now.AddSeconds(-1), null));
        Assert.Equal(count, f.Commits.Count); Assert.Single(f.Telemetry.Snapshots); Assert.Empty(f.Outbox.Items);
        Assert.Equal((short)80, f.Devices.Items[0].CurrentBatteryLevel);
    }
    [Theory]
    [InlineData(-1)] [InlineData(101)]
    public void InvalidBatteryIsRejected(short battery)
    {
        var device = new Device(1001, "1.0");
        Assert.Throws<ArgumentOutOfRangeException>(() => device.RegisterTelemetry(battery, false, DateTime.UtcNow, 15));
    }
    [Fact]
    public void ChargingSuppressesLowBatteryAlert()
    {
        var device = new Device(1001, "1.0"); device.AssignToPatient(25, DateTime.UtcNow);
        device.RegisterTelemetry(5, true, DateTime.UtcNow, 15); Assert.Empty(device.DomainEvents);
    }
    private sealed class Fixture
    {
        public DeviceRepo Devices { get; } = new(); public EventRepo Events { get; } = new(); public OutboxRepo Outbox { get; } = new();
        public Counter Commits { get; } = new(); public Access Access { get; } = new(); public Telemetry Telemetry { get; } = new();
        public DeviceCommandService Service => new(Devices, Events, Outbox, Access, Telemetry, Commits,
            Options.Create(new DeviceMonitoringOptions()), NullLogger<DeviceCommandService>.Instance);
    }
    private class Repo<T> : IBaseRepository<T> where T : class
    {
        public List<T> Items { get; } = [];
        public Task AddAsync(T entity) { Items.Add(entity); return Task.CompletedTask; }
        public virtual Task<T?> FindByIdAsync(long id) => Task.FromResult(Items.FirstOrDefault());
        public void Update(T entity) { } public void Remove(T entity) => Items.Remove(entity);
        public Task<IEnumerable<T>> ListAsync() => Task.FromResult<IEnumerable<T>>(Items);
    }
    private sealed class DeviceRepo : Repo<Device>, IDeviceRepository
    {
        public DeviceRepo() => Items.Add(new Device(1001, "1.0"));
        public override Task<Device?> FindByIdAsync(long id) => Task.FromResult(Items.FirstOrDefault(d => d.DeviceId == id));
        public Task<Device?> FindByAssignedPatientIdAsync(long id) => Task.FromResult(Items.FirstOrDefault(d => d.AssignedPatientId == id));
        public Task<IEnumerable<Device>> FindByPatientIdsAsync(IEnumerable<long> ids) => Task.FromResult(Items.Where(d => d.AssignedPatientId.HasValue && ids.Contains(d.AssignedPatientId.Value)));
        public Task<IReadOnlyCollection<long>> ListMonitoredActiveDeviceIdsAsync() => Task.FromResult<IReadOnlyCollection<long>>(Items.Where(d => d.AssignedPatientId.HasValue).Select(d => d.DeviceId).ToArray());
    }
    private sealed class EventRepo : Repo<DeviceEvent>, IDeviceEventRepository
    {
        public Task<DeviceEvent?> FindLatestUnresolvedByDeviceIdAndTypeAsync(long id, DeviceEventType type) => Task.FromResult(Items.LastOrDefault(e => e.DeviceId == id && e.EventType == type && !e.IsResolved));
        public Task DeleteByDeviceIdAsync(long id) { Items.RemoveAll(e => e.DeviceId == id); return Task.CompletedTask; }
    }
    private sealed class OutboxRepo : Repo<OutboxMessage>, IOutboxMessageRepository
    {
        public Task<IReadOnlyCollection<OutboxMessage>> ListPendingAsync(int size) => Task.FromResult<IReadOnlyCollection<OutboxMessage>>(Items.Where(e => e.ProcessedOn is null).Take(size).ToArray());
    }
    private sealed class Counter : IUnitOfWork { public int Count; public Task CompleteAsync() { Count++; return Task.CompletedTask; } }
    private sealed class Access : IPatientAccessService { public bool Allowed = true; public Task<bool> CanManageDevicesAsync(long actor, long patient) => Task.FromResult(Allowed); }
    private sealed class Telemetry : IDeviceTelemetryRealtimePublisher
    {
        public List<DeviceTelemetrySnapshot> Snapshots { get; } = [];
        public Task PublishTelemetryAsync(DeviceTelemetrySnapshot snapshot, CancellationToken ct = default) { Snapshots.Add(snapshot); return Task.CompletedTask; }
    }
}
