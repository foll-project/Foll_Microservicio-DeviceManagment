using foll_device_managment.Shared.Domain.Model.Entities;

namespace foll_device_managment.Shared.Domain.Repositories;

public interface IOutboxMessageRepository : IBaseRepository<OutboxMessage>
{
    Task<IReadOnlyCollection<OutboxMessage>> ListPendingAsync(int batchSize);
}
