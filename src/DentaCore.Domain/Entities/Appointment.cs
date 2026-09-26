using DentaCore.Domain.Common;
using DentaCore.Domain.Enums;

namespace DentaCore.Domain.Entities;

/// <summary>
/// Appointment booking between a Patient and a Doctor.
/// Status transitions: Pending → Approved/Rejected/Rescheduled → Completed/Cancelled
/// </summary>
public class Appointment : BaseEntity, IAuditable, ISoftDeletable
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime RequestedDateTime { get; set; }
    public DateTime? ConfirmedDateTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? ReasonForVisit { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public Receipt? Receipt { get; set; }
    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();
}
