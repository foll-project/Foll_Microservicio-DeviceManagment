using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;
namespace foll_device_managment.Shared.Infrastructure.Messaging;

public sealed class AccountDeletionCompletedHandler(AppDbContext db) : IBrokerMessageHandler
{
    public async Task HandleAsync(BrokerEnvelope envelope, CancellationToken ct)
    {
        if (envelope.EventType != AccountDeletionCompleted.EventType) throw new InvalidDataException("Evento no soportado.");
        var e = envelope.Read<AccountDeletionCompleted>();
        if (e.UserId <= 0 || e.DeletedPatientIds is null || e.DeletedPatientIds.Any(id => id <= 0))
            throw new InvalidDataException("Confirmacion de Care invalida.");
        var devices = await db.Devices.Where(d => d.AssignedPatientId.HasValue &&
            e.DeletedPatientIds.Contains(d.AssignedPatientId.Value)).ToListAsync(ct);
        foreach (var device in devices) device.Unassign();
    }
}

