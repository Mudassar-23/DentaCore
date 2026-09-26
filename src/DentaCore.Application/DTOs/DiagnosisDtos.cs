namespace DentaCore.Application.DTOs;

public class CreateDiagnosisDto
{
    public string DiseaseName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Prescription { get; set; }
}

public class DiagnosisDto
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public string DiseaseName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Prescription { get; set; }
    public DateTime CreatedAt { get; set; }
}
