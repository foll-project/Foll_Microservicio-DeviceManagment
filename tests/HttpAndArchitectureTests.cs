using System.Net;
using System.Text;
using System.Text.Json;
using foll_device_managment.DeviceManagment.Application.ACL;
using foll_device_managment.DeviceManagment.Interfaces.Internal;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_backend.Shared.Infrastructure.Integration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace Foll.DeviceManagment.Tests;
public class HttpAndArchitectureTests
{
    private const string ServiceKey = "testing-only-service-key-with-at-least-32-characters";
    [Fact]
    public async Task CareCanReadCompleteStatusWithServiceKey()
    {
        using var host = ApiHost(); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Foll-Service-Key", ServiceKey);
        using var response = await client.GetAsync("/internal/devices/patient/25/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1001, json.RootElement.GetProperty("deviceId").GetInt64());
        Assert.True(json.RootElement.GetProperty("isOnline").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("currentBatteryLevel").ValueKind);
        Assert.True(json.RootElement.TryGetProperty("monitoringStartedAt", out _));
    }
    [Theory]
    [InlineData("")] [InlineData("wrong")]
    public async Task InternalStatusDoesNotAcceptMissingOrIncorrectServiceKey(string key)
    {
        using var host = ApiHost(); using var client = host.GetTestClient();
        if (key.Length > 0) client.DefaultRequestHeaders.Add("X-Foll-Service-Key", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/internal/devices/patient/25/status")).StatusCode);
    }
    [Fact]
    public async Task UnknownPatientStatusReturns404AndPublicApiRequiresJwt()
    {
        using var host = ApiHost(); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Foll-Service-Key", ServiceKey);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/internal/devices/patient/999/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices/1001/status")).StatusCode);
    }
    [Fact]
    public void ModelAndExecutableDoNotOwnOtherBoundedContexts()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=test;Password=test").UseSnakeCaseNamingConvention().Options);
        Assert.Equal(3, db.Model.GetEntityTypes().Count());
        Assert.All(db.Model.GetEntityTypes(), e => Assert.Equal("device", e.GetSchema()));
        var assembly = typeof(InternalDevicesController).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name is "foll-backend" or "Foll-Care" or "Foll-EmergencyAnalytics");
        Assert.DoesNotContain(assembly.GetTypes(), t => t.Name.Contains("PatientsDeleted") || t.Name.Contains("UnlinkDevicesByAccount"));
    }
    [Theory]
    [InlineData(false, HttpStatusCode.OK)] [InlineData(true, HttpStatusCode.BadRequest)]
    public async Task TransitionProxyIsOptInAndPreservesRemoteContract(bool remote, HttpStatusCode status)
    {
        using var upstream = GatewayAndDeliveryTests.Client(request =>
        {
            Assert.Equal("/api/devices/1001/link?source=care", request.RequestUri!.PathAndQuery);
            Assert.Equal("Bearer user", request.Headers.Authorization!.ToString());
            Assert.Equal("{\"patientId\":25}", request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("remote-error") };
        });
        using var host = ProxyHost(remote, upstream); using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/devices/1001/link?source=care");
        request.Headers.Authorization = new("Bearer", "user"); request.Content = new StringContent("{\"patientId\":25}", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(remote ? "remote-error" : "local", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task RemoteOutageDoesNotFallBackToObsoleteLocalState()
    {
        using var upstream = GatewayAndDeliveryTests.Client(_ => throw new HttpRequestException("offline"));
        using var host = ProxyHost(true, upstream);
        Assert.Equal(HttpStatusCode.BadGateway, (await host.GetTestClient().GetAsync("/api/devices/1001/status")).StatusCode);
    }
    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static IHost ProxyHost(bool remote, HttpClient client) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddSingleton(Config(new() { ["DeviceManagment:RemoteEnabled"] = remote.ToString() }));
            services.AddSingleton(new DeviceServiceClient(client));
        }).Configure(app => { app.UseMiddleware<DeviceProxyMiddleware>(); app.Run(c => c.Response.WriteAsync("local")); })).Start();
    private static IHost ApiHost() => new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
    {
        services.AddSingleton(Config(new() { ["InternalApi:ApiKey"] = ServiceKey }));
        services.AddSingleton<IDeviceStatusAcl, StatusStub>(); services.AddSingleton<IDeviceAssignmentAcl, AssignmentStub>();
        services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear(); manager.ApplicationParts.Add(new AssemblyPart(typeof(InternalDevicesController).Assembly));
        });
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(ServiceKey)), ValidateAudience = false, ValidateIssuer = false
        });
        services.AddAuthorization();
    }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).Start();
    private sealed class StatusStub : IDeviceStatusAcl
    {
        public Task<DeviceStatusDto?> GetStatusByPatientIdAsync(long id) => Task.FromResult(id == 25
            ? new DeviceStatusDto(1001, 25, "Active", "Connected", null, null, null, DateTime.UtcNow, null, true, false, "1.0") : null);
    }
    private sealed class AssignmentStub : IDeviceAssignmentAcl
    {
        public Task<DeviceAssignmentDto?> GetAssignmentByDeviceIdAsync(long id) => Task.FromResult<DeviceAssignmentDto?>(new(id, 25, "Active", "Connected"));
    }
}
