using DentaCore.Domain.Common;
using DentaCore.Domain.Enums;

namespace DentaCore.Domain.Entities;

/// <summary>
/// Payment settlement record — created when doctor marks a receipt as paid.
/// </summary>
public class Payment : BaseEntity
{
    public Guid ReceiptId { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? TransactionRef { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public Guid UpdatedByDoctorId { get; set; }

    // Navigation
    public Receipt Receipt { get; set; } = null!;
}
