using DentaCore.Domain.Common;

namespace DentaCore.Domain.Entities;

/// <summary>
/// 1:1 profile extension for patients — keeps medical/personal data separate from auth.
/// </summary>
public class Patient : BaseEntity
{
    public Guid UserId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? BloodGroup { get; set; }
    public string? MedicalHistory { get; set; }
    public string? EmergencyContact { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
