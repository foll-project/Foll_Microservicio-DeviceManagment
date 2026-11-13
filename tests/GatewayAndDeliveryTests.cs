using System.Net;
using System.Net.Http.Json;
using foll_device_managment.DeviceManagment.Application.OutboundServices;
using foll_device_managment.DeviceManagment.Application.Ports;
using foll_device_managment.DeviceManagment.Application.Internal.Notifications;
using foll_device_managment.DeviceManagment.Infrastructure.Configuration;
using foll_device_managment.DeviceManagment.Infrastructure.Gateways;
using foll_device_managment.DeviceManagment.Infrastructure.Messaging;
using foll_device_managment.Shared.Domain.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;
namespace Foll.DeviceManagment.Tests;
public class GatewayAndDeliveryTests
{
    [Theory]
    [InlineData(7, 25, true)] [InlineData(8, 25, false)] [InlineData(7, 26, false)]
    public async Task OnlyOfficialGuardianInMatchingCareDtoIsAuthorized(long actor, long patient, bool allowed)
    {
        using var client = Client(request =>
        {
            Assert.Equal("Bearer user-token", request.Headers.Authorization!.ToString());
            Assert.Equal($"/internal/patients/{patient}/device-access", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { patientId = 25, officialGuardianUserId = 7 }) };
        });
        var context = new DefaultHttpContext(); context.Request.Headers.Authorization = "Bearer user-token";
        Assert.Equal(allowed, await new PatientAccessGateway(client, new HttpContextAccessor { HttpContext = context }).CanManageDevicesAsync(actor, patient));
    }
    [Theory]
    [InlineData(HttpStatusCode.NotFound)] [InlineData(HttpStatusCode.Unauthorized)] [InlineData(HttpStatusCode.Forbidden)]
    public async Task CareDenialFailsClosed(HttpStatusCode status)
    {
        using var client = Client(_ => new(status));
        var context = new DefaultHttpContext(); context.Request.Headers.Authorization = "Bearer token";
        Assert.False(await new PatientAccessGateway(client, new HttpContextAccessor { HttpContext = context }).CanManageDevicesAsync(7, 25));
    }
    [Fact]
    public async Task NoTokenDoesNotCallCare()
    {
        using var client = Client(_ => throw new Exception("Must not call"));
        Assert.False(await new PatientAccessGateway(client, new HttpContextAccessor()).CanManageDevicesAsync(7, 25));
    }
    [Fact]
    public async Task CareOutageDoesNotBecomePermissionGranted()
    {
        using var client = Client(_ => new(HttpStatusCode.ServiceUnavailable));
        var context = new DefaultHttpContext(); context.Request.Headers.Authorization = "Bearer token";
        await Assert.ThrowsAsync<HttpRequestException>(() => new PatientAccessGateway(client, new HttpContextAccessor { HttpContext = context }).CanManageDevicesAsync(7, 25));
    }
    [Fact]
    public async Task RepeatedTelemetryKeepsNewestSnapshot()
    {
        var buffer = new LatestTelemetryBuffer(Options.Create(new TelemetryOptions())); var now = DateTime.UtcNow;
        await buffer.PublishTelemetryAsync(Snapshot(1001, now));
        await buffer.PublishTelemetryAsync(Snapshot(1001, now.AddSeconds(1)));
        await buffer.PublishTelemetryAsync(Snapshot(1001, now.AddSeconds(-1)));
        Assert.Equal(now.AddSeconds(1), Assert.Single(buffer.TakeBatch(10, now)).LastHeartbeatAtUtc);
        Assert.Empty(buffer.TakeBatch(10, now));
    }
    [Fact]
    public async Task BufferIsBoundedAndDiscardsExpiredSnapshots()
    {
        var buffer = new LatestTelemetryBuffer(Options.Create(new TelemetryOptions { Capacity = 1, MaxAgeSeconds = 30 }));
        var now = DateTime.UtcNow;
        await buffer.PublishTelemetryAsync(Snapshot(1001, now.AddMinutes(-1)));
        await buffer.PublishTelemetryAsync(Snapshot(1002, now));
        Assert.Equal(1, buffer.Dropped); Assert.Empty(buffer.TakeBatch(10, now));
        await buffer.PublishTelemetryAsync(Snapshot(1002, now)); Assert.Single(buffer.TakeBatch(10, now));
    }
    [Fact]
    public async Task OutboxFailureDoesNotAcknowledgeAndRetryKeepsEventIdentity()
    {
        var gateway = new Gateway { Fail = true };
        var message = OutboxMessage.Create(DeviceManagmentEventTypes.LowBatteryDetectedV1, new { DeviceId = 1001, PatientId = 25 }, DateTime.UtcNow);
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.OutboxMessageId))!.SetValue(message, 41L);
        var delivery = new DeviceEventDelivery(gateway);
        await Assert.ThrowsAsync<HttpRequestException>(() => delivery.DeliverAsync(message, default));
        Assert.Null(message.ProcessedOn);
        gateway.Fail = false; await delivery.DeliverAsync(message, default);
        Assert.NotNull(message.ProcessedOn);
        Assert.Equal(new[] { "device-managment:41", "device-managment:41" }, gateway.EventIds);
    }
    [Fact]
    public async Task OutboxDoesNotDeliverAnotherBoundedContextsMessages()
    {
        var gateway = new Gateway();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DeviceEventDelivery(gateway).DeliverAsync(new OutboxMessage("other-event", "{}", DateTime.UtcNow), default));
        Assert.Empty(gateway.EventIds);
    }
    private static DeviceTelemetrySnapshot Snapshot(long id, DateTime time) => new(id, 25, 80, false, true, time);
    private sealed class Gateway : IDeviceNotificationGateway
    {
        public bool Fail; public List<string> EventIds { get; } = [];
        public Task DeliverEventAsync(DeviceEventEnvelope e, CancellationToken ct)
        {
            EventIds.Add(e.EventId); if (Fail) throw new HttpRequestException("offline"); return Task.CompletedTask;
        }
        public Task DeliverTelemetryAsync(DeviceTelemetrySnapshot snapshot, CancellationToken ct) => Task.CompletedTask;
    }
    internal static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> action) => new(new Handler(action)) { BaseAddress = new Uri("http://test/") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(action(request));
    }
}
