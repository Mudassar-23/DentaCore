using DentaCore.Domain.Enums;

namespace DentaCore.Application.DTOs;

public class CreateAppointmentDto
{
    public Guid DoctorId { get; set; }
    public DateTime RequestedDateTime { get; set; }
    public string? ReasonForVisit { get; set; }
}

public class AppointmentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorSpecialization { get; set; } = string.Empty;
    public DateTime RequestedDateTime { get; set; }
    public DateTime? ConfirmedDateTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? ReasonForVisit { get; set; }
    public string? RejectionReason { get; set; }
    public decimal ConsultationFee { get; set; }
    public Guid? ReceiptId { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public string? PaymentStatusName => PaymentStatus?.ToString();
    public List<DiagnosisDto> Diagnoses { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class ApproveAppointmentDto
{
    public DateTime ConfirmedDateTime { get; set; }
    public decimal? Amount { get; set; }
}

public class RejectAppointmentDto
{
    public string Reason { get; set; } = string.Empty;
}

public class RescheduleAppointmentDto
{
    public DateTime ProposedDateTime { get; set; }
}
