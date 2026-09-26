using DentaCore.Domain.Common;

namespace DentaCore.Domain.Entities;

/// <summary>
/// Diagnosis recorded by a doctor after a completed appointment visit.
/// </summary>
public class Diagnosis : BaseEntity
{
    public Guid AppointmentId { get; set; }
    public string DiseaseName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Prescription { get; set; }

    // Navigation
    public Appointment Appointment { get; set; } = null!;
}
