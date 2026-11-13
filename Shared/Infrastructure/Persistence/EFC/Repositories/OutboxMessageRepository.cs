using foll_device_managment.Shared.Domain.Model.Entities;
using foll_device_managment.Shared.Domain.Repositories;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace foll_device_managment.Shared.Infrastructure.Persistence.EFC.Repositories;

public class OutboxMessageRepository : BaseRepository<OutboxMessage>, IOutboxMessageRepository
{
    public OutboxMessageRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyCollection<OutboxMessage>> ListPendingAsync(int batchSize)
    {
        if (batchSize <= 0) batchSize = 20;

        return await Context.Set<OutboxMessage>()
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOn)
            .ThenBy(m => m.OutboxMessageId)
            .Take(batchSize)
            .ToListAsync();
    }
}
