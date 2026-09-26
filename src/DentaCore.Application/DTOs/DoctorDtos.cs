namespace DentaCore.Application.DTOs;

public class DoctorDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsApprovedByAdmin { get; set; }
    public bool IsActive { get; set; }
}

public class DoctorScheduleDto
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int SlotDurationMinutes { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateDoctorScheduleItemDto
{
    public string DayOfWeek { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int SlotDurationMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;
}

public class UpdateDoctorScheduleRequest
{
    public List<UpdateDoctorScheduleItemDto> Schedules { get; set; } = new();
}

public class AvailableSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAvailable { get; set; }
}
