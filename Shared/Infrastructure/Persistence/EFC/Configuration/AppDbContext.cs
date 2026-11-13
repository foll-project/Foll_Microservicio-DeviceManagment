using foll_device_managment.DeviceManagment.Domain.Model.Entities;
using foll_device_managment.Shared.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceEvent> DeviceEvents => Set<DeviceEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder builder) =>
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
