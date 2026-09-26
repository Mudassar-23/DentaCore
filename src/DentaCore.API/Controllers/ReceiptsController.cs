using DentaCore.Application.Common;
using DentaCore.Application.DTOs;
using DentaCore.Application.Interfaces;
using DentaCore.Domain.Entities;
using DentaCore.Domain.Enums;
using DentaCore.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentaCore.API.Controllers;

[ApiController]
[Route("api/v1/receipts")]
public class ReceiptsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPdfReceiptService _pdfReceiptService;
    private readonly INotificationService _notificationService;

    public ReceiptsController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPdfReceiptService pdfReceiptService,
        INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pdfReceiptService = pdfReceiptService;
        _notificationService = notificationService;
    }

    [HttpGet("{appointmentId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetReceiptByAppointmentId(Guid appointmentId)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Payment)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Doctor)
                    .ThenInclude(d => d.User)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Diagnoses)
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId);

        if (receipt == null)
            return NotFound(new { message = "Receipt not found.", code = ErrorCodes.ReceiptNotFound });

        // Access checks
        if (_currentUserService.Role == AppRoles.Patient && receipt.Appointment.PatientId != _currentUserService.PatientId)
            return Forbid();
        if (_currentUserService.Role == AppRoles.Doctor && receipt.Appointment.DoctorId != _currentUserService.DoctorId)
            return Forbid();

        var appt = receipt.Appointment;
        var diag = appt.Diagnoses.OrderByDescending(d => d.CreatedAt).FirstOrDefault();

        var dto = new ReceiptDto
        {
            Id = receipt.Id,
            AppointmentId = receipt.AppointmentId,
            ReceiptNumber = receipt.ReceiptNumber,
            Amount = receipt.Amount,
            PaymentStatus = receipt.PaymentStatus,
            GeneratedAt = receipt.GeneratedAt,
            DoctorName = appt.Doctor.User.FullName,
            DoctorSpecialization = appt.Doctor.Specialization,
            DoctorLicense = appt.Doctor.LicenseNumber,
            PatientName = appt.Patient.User.FullName,
            PatientEmail = appt.Patient.User.Email,
            PatientPhone = appt.Patient.User.PhoneNumber,
            ConfirmedDateTime = appt.ConfirmedDateTime,
            ReasonForVisit = appt.ReasonForVisit,
            DiseaseName = diag?.DiseaseName,
            ClinicalNotes = diag?.Notes,
            Prescription = diag?.Prescription,
            AmountPaid = receipt.Payment?.AmountPaid,
            PaymentMethod = receipt.Payment?.PaymentMethod,
            TransactionRef = receipt.Payment?.TransactionRef,
            PaidAt = receipt.Payment?.PaidAt
        };

        return Ok(dto);
    }

    [HttpGet("{appointmentId:guid}/download")]
    [Authorize]
    public async Task<IActionResult> DownloadReceiptPdf(Guid appointmentId)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Appointment)
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId);

        if (receipt == null)
            return NotFound(new { message = "Receipt not found.", code = ErrorCodes.ReceiptNotFound });

        // Access checks
        if (_currentUserService.Role == AppRoles.Patient && receipt.Appointment.PatientId != _currentUserService.PatientId)
            return Forbid();
        if (_currentUserService.Role == AppRoles.Doctor && receipt.Appointment.DoctorId != _currentUserService.DoctorId)
            return Forbid();

        var pdfBytes = await _pdfReceiptService.GenerateReceiptPdfBytesAsync(receipt.Id);

        return File(pdfBytes, "application/pdf", $"{receipt.ReceiptNumber}.pdf");
    }

    [HttpPut("{appointmentId:guid}/mark-paid")]
    [Authorize(Roles = $"{AppRoles.Doctor},{AppRoles.Admin}")]
    public async Task<IActionResult> MarkPaymentPaid(Guid appointmentId, [FromBody] MarkPaymentDto request)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Payment)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Patient)
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId);

        if (receipt == null)
            return NotFound(new { message = "Receipt not found.", code = ErrorCodes.ReceiptNotFound });

        if (_currentUserService.Role == AppRoles.Doctor && receipt.Appointment.DoctorId != _currentUserService.DoctorId)
            return Forbid();

        receipt.PaymentStatus = PaymentStatus.Paid;

        if (receipt.Payment == null)
        {
            var payment = new Payment
            {
                ReceiptId = receipt.Id,
                AmountPaid = request.AmountPaid,
                PaymentMethod = request.PaymentMethod,
                TransactionRef = request.TransactionRef ?? $"TXN-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                PaidAt = DateTime.UtcNow,
                UpdatedByDoctorId = _currentUserService.DoctorId ?? Guid.Empty
            };
            _context.Payments.Add(payment);
        }
        else
        {
            receipt.Payment.AmountPaid = request.AmountPaid;
            receipt.Payment.PaymentMethod = request.PaymentMethod;
            receipt.Payment.TransactionRef = request.TransactionRef ?? receipt.Payment.TransactionRef;
            receipt.Payment.PaidAt = DateTime.UtcNow;
            receipt.Payment.UpdatedByDoctorId = _currentUserService.DoctorId ?? Guid.Empty;
        }

        await _context.SaveChangesAsync();

        // Push live payment notification to patient (Blueprint 4.2.7)
        await _notificationService.NotifyUserAsync(
            receipt.Appointment.Patient.UserId,
            "Payment Received",
            $"Payment of ${request.AmountPaid:N2} for receipt #{receipt.ReceiptNumber} has been recorded. Your updated receipt is now available with PAID status.");

        return Ok(new
        {
            message = "Payment successfully recorded and receipt marked as Paid.",
            paymentStatus = receipt.PaymentStatus.ToString(),
            amountPaid = request.AmountPaid
        });
    }

    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> GetMyReceipts()
    {
        var patientId = _currentUserService.PatientId;
        if (!patientId.HasValue) return Forbid();

        var list = await _context.Receipts
            .Include(r => r.Payment)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Doctor)
                    .ThenInclude(d => d.User)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Diagnoses)
            .Where(r => r.Appointment.PatientId == patientId.Value)
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => new ReceiptDto
            {
                Id = r.Id,
                AppointmentId = r.AppointmentId,
                ReceiptNumber = r.ReceiptNumber,
                Amount = r.Amount,
                PaymentStatus = r.PaymentStatus,
                GeneratedAt = r.GeneratedAt,
                DoctorName = r.Appointment.Doctor.User.FullName,
                DoctorSpecialization = r.Appointment.Doctor.Specialization,
                DoctorLicense = r.Appointment.Doctor.LicenseNumber,
                PatientName = r.Appointment.Patient.User.FullName,
                PatientEmail = r.Appointment.Patient.User.Email,
                PatientPhone = r.Appointment.Patient.User.PhoneNumber,
                ConfirmedDateTime = r.Appointment.ConfirmedDateTime,
                ReasonForVisit = r.Appointment.ReasonForVisit,
                AmountPaid = r.Payment != null ? r.Payment.AmountPaid : null,
                PaymentMethod = r.Payment != null ? r.Payment.PaymentMethod : null,
                TransactionRef = r.Payment != null ? r.Payment.TransactionRef : null,
                PaidAt = r.Payment != null ? r.Payment.PaidAt : null
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GetAllReceipts([FromQuery] string? status)
    {
        var query = _context.Receipts
            .Include(r => r.Payment)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PaymentStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(r => r.PaymentStatus == parsedStatus);
        }

        var list = await query
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => new ReceiptDto
            {
                Id = r.Id,
                AppointmentId = r.AppointmentId,
                ReceiptNumber = r.ReceiptNumber,
                Amount = r.Amount,
                PaymentStatus = r.PaymentStatus,
                GeneratedAt = r.GeneratedAt,
                DoctorName = r.Appointment.Doctor.User.FullName,
                DoctorSpecialization = r.Appointment.Doctor.Specialization,
                DoctorLicense = r.Appointment.Doctor.LicenseNumber,
                PatientName = r.Appointment.Patient.User.FullName,
                PatientEmail = r.Appointment.Patient.User.Email,
                PatientPhone = r.Appointment.Patient.User.PhoneNumber,
                ConfirmedDateTime = r.Appointment.ConfirmedDateTime,
                ReasonForVisit = r.Appointment.ReasonForVisit,
                AmountPaid = r.Payment != null ? r.Payment.AmountPaid : null,
                PaymentMethod = r.Payment != null ? r.Payment.PaymentMethod : null,
                TransactionRef = r.Payment != null ? r.Payment.TransactionRef : null,
                PaidAt = r.Payment != null ? r.Payment.PaidAt : null
            })
            .ToListAsync();

        var totalRevenue = list.Where(r => r.PaymentStatus == PaymentStatus.Paid).Sum(r => r.Amount);
        var totalPending = list.Where(r => r.PaymentStatus == PaymentStatus.Pending).Sum(r => r.Amount);

        return Ok(new
        {
            totalReceipts = list.Count,
            totalRevenue,
            totalPending,
            receipts = list
        });
    }
}
