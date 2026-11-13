using System.Text.Json;
using foll_device_managment.DeviceManagment.Application.Ports;
using foll_device_managment.DeviceManagment.Application.Internal.Notifications;
using foll_device_managment.Shared.Domain.Model.Entities;
using foll_device_managment.Shared.Domain.Repositories;
using foll_device_managment.Shared.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace foll_device_managment.DeviceManagment.Infrastructure.Messaging;

public class DeviceEventDelivery(IDeviceNotificationGateway gateway)
{
    public async Task DeliverAsync(OutboxMessage message, CancellationToken ct)
    {
        if (!SupportedTypes.Contains(message.Type)) throw new InvalidOperationException("Evento ajeno a Devices.");
        using var payload = JsonDocument.Parse(message.Payload);
        await gateway.DeliverEventAsync(new DeviceEventEnvelope($"device-managment:{message.OutboxMessageId}",
            message.OutboxMessageId, message.Type, payload.RootElement.Clone()), ct);
        message.MarkProcessed(DateTime.UtcNow);
    }
    public static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string>
    {
        DeviceManagmentEventTypes.LowBatteryDetectedV1, DeviceManagmentEventTypes.LowBatteryResolvedV1,
        DeviceManagmentEventTypes.DeviceDisconnectedV1, DeviceManagmentEventTypes.DeviceReconnectedV1
    };
}
public class OutboxPublisherBackgroundService(IServiceScopeFactory scopes, IOptions<OutboxOptions> options,
    ILogger<OutboxPublisherBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var delivery = scope.ServiceProvider.GetRequiredService<DeviceEventDelivery>();
                var messages = await repository.ListPendingAsync(options.Value.BatchSize);
                foreach (var message in messages)
                {
                    try { await delivery.DeliverAsync(message, ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        message.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
                        logger.LogError(ex, "Entrega {MessageId} fallida; se reintentará.", message.OutboxMessageId);
                        break; // Do not deliver a recovery before its failed alert.
                    }
                }
                if (messages.Count > 0) await unitOfWork.CompleteAsync();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error procesando outbox de dispositivos."); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
