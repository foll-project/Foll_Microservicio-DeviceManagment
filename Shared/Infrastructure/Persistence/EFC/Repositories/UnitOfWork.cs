using foll_device_managment.Shared.Domain.Repositories;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;

namespace foll_device_managment.Shared.Infrastructure.Persistence.EFC.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task CompleteAsync() => await _context.SaveChangesAsync();
}
