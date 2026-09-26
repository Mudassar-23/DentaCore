namespace DentaCore.Application.Common;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    Guid? DoctorId { get; }
    Guid? PatientId { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
}
