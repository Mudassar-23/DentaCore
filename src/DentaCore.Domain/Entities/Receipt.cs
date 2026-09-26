using DentaCore.Domain.Common;
using DentaCore.Domain.Enums;

namespace DentaCore.Domain.Entities;

/// <summary>
/// Receipt generated when an appointment is approved. 
/// PDF is re-generated when payment status changes.
/// </summary>
public class Receipt : BaseEntity
{
    public Guid AppointmentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? PdfFilePath { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Appointment Appointment { get; set; } = null!;
    public Payment? Payment { get; set; }
}
