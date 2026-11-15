using foll_device_managment.DeviceManagment.Application.ACL;
using foll_device_managment.Shared.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace foll_device_managment.DeviceManagment.Interfaces.Internal;

[ApiController]
[Route("internal/devices")]
public class InternalDevicesController(IDeviceStatusAcl status, IDeviceAssignmentAcl assignments) : ControllerBase
{
    [HttpGet("patient/{patientId:long}/status")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Status(long patientId)
    {
        var result = await status.GetStatusByPatientIdAsync(patientId);
        return result is null ? NotFound(new { message = "No hay dispositivo vinculado a este paciente." }) : Ok(result);
    }
    [HttpGet("{deviceId:long}/assignment")]
    [EmergencyAssignmentKey]
    public async Task<IActionResult> Assignment(long deviceId)
    {
        var result = await assignments.GetAssignmentByDeviceIdAsync(deviceId);
        return result is null ? NotFound() : Ok(result);
    }
}
