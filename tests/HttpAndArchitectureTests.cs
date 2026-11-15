using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using foll_device_managment.DeviceManagment.Application.ACL;
using foll_device_managment.DeviceManagment.Interfaces.Internal;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;

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
    private const string SigningKey = "testing-only-service-key-with-at-least-32-characters";
    [Fact]
    public async Task CareCanReadCompleteStatusWithJwt()
    {
        using var host = ApiHost(); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
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
    public async Task InternalStatusDoesNotAcceptMissingOrIncorrectJwt(string key)
    {
        using var host = ApiHost(); using var client = host.GetTestClient();
        if (key.Length > 0) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/internal/devices/patient/25/status")).StatusCode);
    }
    [Fact]
    public async Task UnknownPatientStatusReturns404AndPublicApiRequiresJwt()
    {
        using var host = ApiHost(); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/internal/devices/patient/999/status")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices/1001/status")).StatusCode);
    }
    [Fact]
    public void ModelAndExecutableDoNotOwnOtherBoundedContexts()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=test;Password=test").UseSnakeCaseNamingConvention().Options);
        Assert.Equal(4, db.Model.GetEntityTypes().Count());
        Assert.Contains(db.Model.GetEntityTypes(), e => e.GetTableName() == "integration_inbox_messages");
        Assert.All(db.Model.GetEntityTypes(), e => Assert.Equal("device", e.GetSchema()));
        var assembly = typeof(InternalDevicesController).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name is "foll-backend" or "Foll-Care" or "Foll-EmergencyAnalytics");
        Assert.DoesNotContain(assembly.GetTypes(), t => t.Name.Contains("PatientsDeleted") || t.Name.Contains("UnlinkDevicesByAccount"));
    }
    [Fact]
    public async Task AssignmentKeyDoesNotGrantStatusAndJwtAloneDoesNotGrantAssignment()
    {
        using var host = ApiHost(); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Foll-Service-Key", "test-only-emergency-assignment-key-at-least-32-bytes");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/internal/devices/1001/assignment")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/internal/devices/patient/25/status")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Foll-Service-Key");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/internal/devices/1001/assignment")).StatusCode);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExpiredOrWronglySignedJwtIsRejected(bool expired)
    {
        using var host = ApiHost(); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            CreateToken(expired: expired, signingKey: expired ? SigningKey : new string('x', 64)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/internal/devices/patient/25/status")).StatusCode);
    }
    private static string CreateToken(bool expired = false, string signingKey = SigningKey)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(claims: new[] { new Claim("userId", "7") },
            notBefore: now.AddHours(-2), expires: expired ? now.AddHours(-1) : now.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static IHost ApiHost() => new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
    {
        services.AddSingleton<IConfiguration>(Config(new() { ["InternalApi:EmergencyAssignmentKey"] = "test-only-emergency-assignment-key-at-least-32-bytes" }));
        services.AddSingleton<IDeviceStatusAcl, StatusStub>(); services.AddSingleton<IDeviceAssignmentAcl, AssignmentStub>();
        services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear(); manager.ApplicationParts.Add(new AssemblyPart(typeof(InternalDevicesController).Assembly));
        });
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(SigningKey)), ValidateAudience = false, ValidateIssuer = false, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
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
