using System.Net;
using System.Net.Http.Headers;
using foll_device_managment.DeviceManagment.Application.OutboundServices;

namespace foll_device_managment.DeviceManagment.Infrastructure.Gateways;

public class PatientAccessGateway(HttpClient client, IHttpContextAccessor accessor) : IPatientAccessService
{
    public async Task<bool> CanManageDevicesAsync(long actorUserId, long patientId)
    {
        if (actorUserId <= 0 || patientId <= 0) return false;
        var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)) return false;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/patients/{patientId}/device-access");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(header);
        using var response = await client.SendAsync(request);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) return false;
        response.EnsureSuccessStatusCode();
        var access = await response.Content.ReadFromJsonAsync<PatientAccess>();
        return access?.PatientId == patientId && access.OfficialGuardianUserId == actorUserId;
    }
    private sealed record PatientAccess(long PatientId, long OfficialGuardianUserId);
}
