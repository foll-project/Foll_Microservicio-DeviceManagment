using foll_device_managment.DeviceManagment.Domain.Model.Commands;
using foll_device_managment.DeviceManagment.Domain.Services;
namespace foll_device_managment.DeviceManagment.Application.Internal.CommandServices;
public sealed class DeviceCommandLocks : IDisposable
{
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    public SemaphoreSlim For(long id) => _locks[(int)((ulong)id % (uint)_locks.Length)];
    public void Dispose() { foreach (var gate in _locks) gate.Dispose(); }
}
public class SerializedDeviceCommands(DeviceCommandService commands, DeviceCommandLocks locks) : IDeviceCommandService
{
    private async Task Run(long id, Func<Task> action)
    {
        var gate = locks.For(id); await gate.WaitAsync();
        try { await action(); } finally { gate.Release(); }
    }
    public Task Handle(LinkDeviceCommand c) => Run(c.DeviceId, () => commands.Handle(c));
    public Task Handle(UnlinkDeviceCommand c) => Run(c.DeviceId, () => commands.Handle(c));
    public Task Handle(RegisterDeviceTelemetryCommand c) => Run(c.DeviceId, () => commands.Handle(c));
    public Task Handle(CheckDeviceConnectivityCommand c) => Run(c.DeviceId, () => commands.Handle(c));
    public Task Handle(UpdateDevicePowerStateCommand c) => Run(c.DeviceId, () => commands.Handle(c));
}
