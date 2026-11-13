namespace foll_device_managment.Shared.Domain.Repositories;

public interface IUnitOfWork
{
    Task CompleteAsync();
}
