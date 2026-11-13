using System.Text;
using foll_device_managment.DeviceManagment.Application.ACL;
using foll_device_managment.DeviceManagment.Application.Internal.CommandServices;
using foll_device_managment.DeviceManagment.Application.Internal.QueryServices;
using foll_device_managment.DeviceManagment.Application.OutboundServices;
using foll_device_managment.DeviceManagment.Application.Ports;
using foll_device_managment.DeviceManagment.Domain.Repositories;
using foll_device_managment.DeviceManagment.Domain.Services;
using foll_device_managment.DeviceManagment.Infrastructure.Configuration;
using foll_device_managment.DeviceManagment.Infrastructure.Gateways;
using foll_device_managment.DeviceManagment.Infrastructure.Messaging;
using foll_device_managment.DeviceManagment.Infrastructure.Persistence.EFC.Repositories;
using foll_device_managment.Shared.Domain.Repositories;
using foll_device_managment.Shared.Infrastructure.Configuration;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_device_managment.Shared.Infrastructure.Persistence.EFC.Repositories;
using foll_device_managment.Shared.Infrastructure.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
var builder = WebApplication.CreateBuilder(args);
var secret = builder.Configuration["JwtSettings:Secret"];
var serviceKey = builder.Configuration["InternalApi:ApiKey"];
if (string.IsNullOrWhiteSpace(secret) || Encoding.ASCII.GetByteCount(secret) < 32)
    throw new InvalidOperationException("Configure JwtSettings:Secret de IAM (al menos 32 bytes).");
if (string.IsNullOrWhiteSpace(serviceKey) || serviceKey.Length < 32)
    throw new InvalidOperationException("Configure InternalApi:ApiKey con al menos 32 caracteres.");
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "device"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
        ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Foll DeviceManagment v2", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    options.OperationFilter<AuthorizeOperationFilter>();
});
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddOptions<DeviceMonitoringOptions>().Bind(builder.Configuration.GetSection("DeviceMonitoring"))
    .Validate(o => o.LowBatteryThreshold is > 0 and <= 100 && o.ExpectedHeartbeatIntervalSeconds > 0 &&
        o.MissedHeartbeatsBeforeDisconnect > 0 && o.ConnectivityCheckIntervalSeconds > 0, "Opciones de monitoreo inválidas.").ValidateOnStart();
builder.Services.AddOptions<OutboxOptions>().Bind(builder.Configuration.GetSection("Outbox"))
    .Validate(o => o.BatchSize > 0 && o.PollIntervalSeconds > 0).ValidateOnStart();
builder.Services.AddOptions<TelemetryOptions>().Bind(builder.Configuration.GetSection("Telemetry"))
    .Validate(o => o.Capacity > 0 && o.MaxAgeSeconds > 0 && o.PollIntervalMilliseconds > 0).ValidateOnStart();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IDeviceEventRepository, DeviceEventRepository>();
builder.Services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
builder.Services.AddScoped<DeviceCommandService>();
builder.Services.AddSingleton<DeviceCommandLocks>();
builder.Services.AddScoped<IDeviceCommandService, SerializedDeviceCommands>();
builder.Services.AddScoped<IDeviceQueryService, DeviceQueryService>();
builder.Services.AddScoped<IDeviceAssignmentAcl, DeviceAssignmentAcl>();
builder.Services.AddScoped<IDeviceStatusAcl, DeviceStatusAcl>();
builder.Services.AddScoped<DeviceEventDelivery>();
builder.Services.AddSingleton<LatestTelemetryBuffer>();
builder.Services.AddSingleton<IDeviceTelemetryRealtimePublisher>(sp => sp.GetRequiredService<LatestTelemetryBuffer>());
builder.Services.AddHttpClient<IPatientAccessService, PatientAccessGateway>(client => ConfigureClient(client, "Care", false));
builder.Services.AddHttpClient<IDeviceNotificationGateway, DeviceNotificationGateway>(client => ConfigureClient(client, "Notifications", true));
if (builder.Configuration.GetValue("Mqtt:Enabled", true)) builder.Services.AddHostedService<MqttHeartbeatSubscriberBackgroundService>();
if (builder.Configuration.GetValue("DeviceMonitoring:Enabled", true)) builder.Services.AddHostedService<DeviceConnectivityMonitorBackgroundService>();
if (builder.Configuration.GetValue("Outbox:Enabled", true)) builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
if (builder.Configuration.GetValue("Telemetry:Enabled", true)) builder.Services.AddHostedService<TelemetryPublisherBackgroundService>();
var app = builder.Build();
if (builder.Configuration.GetValue("ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled")) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { service = "DeviceManagment", status = "alive" }));
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Devices.AsNoTracking().AnyAsync(ct); await db.DeviceEvents.AsNoTracking().AnyAsync(ct); await db.OutboxMessages.AsNoTracking().AnyAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch { return Results.StatusCode(503); }
});
app.MapControllers(); app.Run();
void ConfigureClient(HttpClient client, string name, bool internalAuthentication)
{
    var url = builder.Configuration[$"Services:{name}:BaseUrl"] ?? throw new InvalidOperationException($"Falta URL de {name}.");
    client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue($"Services:{name}:TimeoutSeconds", 10));
    if (internalAuthentication) client.DefaultRequestHeaders.Add("X-Foll-Service-Key", serviceKey);
}
public partial class Program;
