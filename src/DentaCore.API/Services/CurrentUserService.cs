using System.Security.Claims;
using DentaCore.Application.Common;
using DentaCore.Shared.Constants;

namespace DentaCore.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var idClaim = HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string? Email => HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public string? Role => HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? HttpContext?.User.FindFirstValue("role");

    public Guid? DoctorId
    {
        get
        {
            var doctorIdClaim = HttpContext?.User.FindFirstValue("doctorId") ?? HttpContext?.User.FindFirstValue("profileId");
            if (string.Equals(Role, AppRoles.Doctor, StringComparison.OrdinalIgnoreCase) && Guid.TryParse(doctorIdClaim, out var id))
                return id;
            return null;
        }
    }

    public Guid? PatientId
    {
        get
        {
            var patientIdClaim = HttpContext?.User.FindFirstValue("patientId") ?? HttpContext?.User.FindFirstValue("profileId");
            if (string.Equals(Role, AppRoles.Patient, StringComparison.OrdinalIgnoreCase) && Guid.TryParse(patientIdClaim, out var id))
                return id;
            return null;
        }
    }

    public string? IpAddress => HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public bool IsAuthenticated => HttpContext?.User.Identity?.IsAuthenticated == true;
}
