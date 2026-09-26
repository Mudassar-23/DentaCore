using DentaCore.Domain.Common;

namespace DentaCore.Domain.Entities;

/// <summary>
/// 1:1 profile extension for doctors — specialization, fees, admin approval.
/// </summary>
public class Doctor : BaseEntity
{
    public Guid UserId { get; set; }
    public string Specialization { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsApprovedByAdmin { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
}
