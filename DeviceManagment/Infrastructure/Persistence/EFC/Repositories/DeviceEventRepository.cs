using foll_device_managment.DeviceManagment.Domain.Model.Entities;
using foll_device_managment.DeviceManagment.Domain.Model.Enums;
using foll_device_managment.DeviceManagment.Domain.Repositories;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace foll_device_managment.DeviceManagment.Infrastructure.Persistence.EFC.Repositories;

public class DeviceEventRepository : BaseRepository<DeviceEvent>, IDeviceEventRepository
{
    public DeviceEventRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<DeviceEvent?> FindLatestUnresolvedByDeviceIdAndTypeAsync(long deviceId, DeviceEventType eventType)
    {
        if (deviceId <= 0) return null;

        return await Context.Set<DeviceEvent>()
            .Where(e => e.DeviceId == deviceId && e.EventType == eventType && !e.IsResolved)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteByDeviceIdAsync(long deviceId)
    {
        if (deviceId <= 0) return;

        var events = await Context.Set<DeviceEvent>()
            .Where(e => e.DeviceId == deviceId)
            .ToListAsync();

        if (events.Any())
        {
            Context.Set<DeviceEvent>().RemoveRange(events);
        }
    }
}
