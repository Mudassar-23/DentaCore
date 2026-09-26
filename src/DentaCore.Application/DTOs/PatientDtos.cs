namespace DentaCore.Application.DTOs;

public class PatientDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? BloodGroup { get; set; }
    public string? MedicalHistory { get; set; }
    public string? EmergencyContact { get; set; }
    public bool IsActive { get; set; }
}

public class UpdatePatientDto
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? BloodGroup { get; set; }
    public string? MedicalHistory { get; set; }
    public string? EmergencyContact { get; set; }
}
