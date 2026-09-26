using DentaCore.Domain.Enums;

namespace DentaCore.Application.DTOs;

public class ReceiptDto
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string PaymentStatusName => PaymentStatus.ToString();
    public DateTime GeneratedAt { get; set; }

    // Enriched visit information for invoices & receipts
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorSpecialization { get; set; } = string.Empty;
    public string DoctorLicense { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public DateTime? ConfirmedDateTime { get; set; }
    public string? ReasonForVisit { get; set; }
    public string? DiseaseName { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? Prescription { get; set; }

    // Settlement details if paid
    public decimal? AmountPaid { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentMethodName => PaymentMethod?.ToString();
    public string? TransactionRef { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class MarkPaymentDto
{
    public decimal AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? TransactionRef { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? TransactionRef { get; set; }
    public DateTime PaidAt { get; set; }
    public Guid UpdatedByDoctorId { get; set; }
}
