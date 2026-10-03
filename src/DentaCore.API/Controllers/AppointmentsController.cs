using DentaCore.Application.Common;
using DentaCore.Application.DTOs;
using DentaCore.Application.Interfaces;
using DentaCore.Domain.Entities;
using DentaCore.Domain.Enums;
using DentaCore.Shared.Constants;
using DentaCore.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentaCore.API.Controllers;

[ApiController]
[Route("api/v1/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public AppointmentsController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> BookAppointment([FromBody] CreateAppointmentDto request)
    {
        var patientId = _currentUserService.PatientId;
        if (!patientId.HasValue) return Forbid();

        var doctor = await _context.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == request.DoctorId && d.IsApprovedByAdmin);

        if (doctor == null)
            return BadRequest(new { message = "Selected doctor is not available.", code = ErrorCodes.DoctorNotApproved });

        // Double-booking check: prevent conflicting approved or pending slot within 30 min window
        var slotWindowStart = request.RequestedDateTime.AddMinutes(-29);
        var slotWindowEnd = request.RequestedDateTime.AddMinutes(29);

        var isBooked = await _context.Appointments.AnyAsync(a =>
            a.DoctorId == request.DoctorId &&
            a.Status != AppointmentStatus.Cancelled &&
            a.Status != AppointmentStatus.Rejected &&
            ((a.ConfirmedDateTime.HasValue && a.ConfirmedDateTime.Value >= slotWindowStart && a.ConfirmedDateTime.Value <= slotWindowEnd) ||
             (a.RequestedDateTime >= slotWindowStart && a.RequestedDateTime <= slotWindowEnd)));

        if (isBooked)
        {
            return BadRequest(new { message = "The selected time slot is no longer available. Please choose another time.", code = ErrorCodes.SlotUnavailable });
        }

        var appointment = new Appointment
        {
            PatientId = patientId.Value,
            DoctorId = request.DoctorId,
            RequestedDateTime = request.RequestedDateTime,
            Status = AppointmentStatus.Pending,
            ReasonForVisit = request.ReasonForVisit
        };

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        // Notify Doctor
        await _notificationService.NotifyUserAsync(
            doctor.UserId,
            "New Appointment Request",
            $"A patient has requested an appointment for {request.RequestedDateTime:dd MMM yyyy, hh:mm tt}.");

        return CreatedAtAction(nameof(GetAppointmentById), new { id = appointment.Id }, new { id = appointment.Id, status = appointment.Status.ToString() });
    }

    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> GetMyAppointments()
    {
        var patientId = _currentUserService.PatientId;
        if (!patientId.HasValue && _currentUserService.UserId.HasValue)
        {
            var p = await _context.Patients.FirstOrDefaultAsync(x => x.UserId == _currentUserService.UserId.Value);
            patientId = p?.Id;
        }

        if (!patientId.HasValue)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Patient profile not found for this account." });

        var list = await _context.Appointments
            .Include(a => a.Doctor)
                .ThenInclude(d => d.User)
            .Include(a => a.Receipt)
            .Include(a => a.Diagnoses)
            .Where(a => a.PatientId == patientId.Value)
            .OrderByDescending(a => a.RequestedDateTime)
            .Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                PatientName = "",
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor.User.FullName,
                DoctorSpecialization = a.Doctor.Specialization,
                RequestedDateTime = a.RequestedDateTime,
                ConfirmedDateTime = a.ConfirmedDateTime,
                Status = a.Status,
                ReasonForVisit = a.ReasonForVisit,
                RejectionReason = a.RejectionReason,
                ConsultationFee = a.Doctor.ConsultationFee,
                ReceiptId = a.Receipt != null ? a.Receipt.Id : null,
                PaymentStatus = a.Receipt != null ? a.Receipt.PaymentStatus : null,
                Diagnoses = a.Diagnoses.Select(d => new DiagnosisDto
                {
                    Id = d.Id,
                    AppointmentId = d.AppointmentId,
                    DiseaseName = d.DiseaseName,
                    Notes = d.Notes,
                    Prescription = d.Prescription,
                    CreatedAt = d.CreatedAt
                }).ToList(),
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("doctor/me")]
    [Authorize(Roles = AppRoles.Doctor)]
    public async Task<IActionResult> GetDoctorQueue([FromQuery] string? status)
    {
        var doctorId = _currentUserService.DoctorId;
        if (!doctorId.HasValue && _currentUserService.UserId.HasValue)
        {
            var doc = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == _currentUserService.UserId.Value);
            doctorId = doc?.Id;
        }

        if (!doctorId.HasValue)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Doctor profile not found for this account." });

        var query = _context.Appointments
            .Include(a => a.Patient)
                .ThenInclude(p => p.User)
            .Include(a => a.Doctor)
                .ThenInclude(d => d.User)
            .Include(a => a.Receipt)
            .Include(a => a.Diagnoses)
            .Where(a => a.DoctorId == doctorId.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "Today", StringComparison.OrdinalIgnoreCase))
            {
                var todayStart = DateTime.UtcNow.Date;
                var todayEnd = todayStart.AddDays(1).AddTicks(-1);
                query = query.Where(a => (a.ConfirmedDateTime.HasValue && a.ConfirmedDateTime.Value >= todayStart && a.ConfirmedDateTime.Value <= todayEnd) ||
                                         (a.RequestedDateTime >= todayStart && a.RequestedDateTime <= todayEnd));
            }
            else if (Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(a => a.Status == parsedStatus);
            }
        }

        var list = await query
            .OrderByDescending(a => a.RequestedDateTime)
            .Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                PatientName = a.Patient.User.FullName,
                PatientPhone = a.Patient.User.PhoneNumber,
                PatientEmail = a.Patient.User.Email,
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor.User.FullName,
                DoctorSpecialization = a.Doctor.Specialization,
                RequestedDateTime = a.RequestedDateTime,
                ConfirmedDateTime = a.ConfirmedDateTime,
                Status = a.Status,
                ReasonForVisit = a.ReasonForVisit,
                RejectionReason = a.RejectionReason,
                ConsultationFee = a.Doctor.ConsultationFee,
                ReceiptId = a.Receipt != null ? a.Receipt.Id : null,
                PaymentStatus = a.Receipt != null ? a.Receipt.PaymentStatus : null,
                Diagnoses = a.Diagnoses.Select(d => new DiagnosisDto
                {
                    Id = d.Id,
                    AppointmentId = d.AppointmentId,
                    DiseaseName = d.DiseaseName,
                    Notes = d.Notes,
                    Prescription = d.Prescription,
                    CreatedAt = d.CreatedAt
                }).ToList(),
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetAppointmentById(Guid id)
    {
        var a = await _context.Appointments
            .Include(x => x.Patient)
                .ThenInclude(p => p.User)
            .Include(x => x.Doctor)
                .ThenInclude(d => d.User)
            .Include(x => x.Receipt)
            .Include(x => x.Diagnoses)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a == null) return NotFound(new { message = "Appointment not found.", code = ErrorCodes.AppointmentNotFound });

        // Resource authorization check
        if (_currentUserService.Role == AppRoles.Patient && a.PatientId != _currentUserService.PatientId)
            return Forbid();
        if (_currentUserService.Role == AppRoles.Doctor && a.DoctorId != _currentUserService.DoctorId)
            return Forbid();

        return Ok(new AppointmentDto
        {
            Id = a.Id,
            PatientId = a.PatientId,
            PatientName = a.Patient.User.FullName,
            PatientPhone = a.Patient.User.PhoneNumber,
            PatientEmail = a.Patient.User.Email,
            DoctorId = a.DoctorId,
            DoctorName = a.Doctor.User.FullName,
            DoctorSpecialization = a.Doctor.Specialization,
            RequestedDateTime = a.RequestedDateTime,
            ConfirmedDateTime = a.ConfirmedDateTime,
            Status = a.Status,
            ReasonForVisit = a.ReasonForVisit,
            RejectionReason = a.RejectionReason,
            ConsultationFee = a.Doctor.ConsultationFee,
            ReceiptId = a.Receipt?.Id,
            PaymentStatus = a.Receipt?.PaymentStatus,
            Diagnoses = a.Diagnoses.Select(d => new DiagnosisDto
            {
                Id = d.Id,
                AppointmentId = d.AppointmentId,
                DiseaseName = d.DiseaseName,
                Notes = d.Notes,
                Prescription = d.Prescription,
                CreatedAt = d.CreatedAt
            }).ToList(),
            CreatedAt = a.CreatedAt
        });
    }

    [HttpPut("{id:guid}/approve")]
    [Authorize(Roles = $"{AppRoles.Doctor},{AppRoles.Admin}")]
    public async Task<IActionResult> ApproveAppointment(Guid id, [FromBody] ApproveAppointmentDto request)
    {
        var role = _currentUserService.Role;
        var doctorId = _currentUserService.DoctorId;

        var appt = await _context.Appointments
            .Include(a => a.Doctor)
            .Include(a => a.Patient)
            .Include(a => a.Receipt)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appt == null) return NotFound(new { message = "Appointment not found." });

        if (role == AppRoles.Doctor && (!doctorId.HasValue || appt.DoctorId != doctorId.Value))
            return Forbid();

        if (appt.Status != AppointmentStatus.Pending && appt.Status != AppointmentStatus.Rescheduled)
        {
            return BadRequest(new { message = $"Cannot approve appointment with current status {appt.Status}.", code = ErrorCodes.InvalidStatusTransition });
        }

        appt.ConfirmedDateTime = request.ConfirmedDateTime;
        appt.Status = AppointmentStatus.Approved;

        // Auto-generate Receipt upon approval (Blueprint 4.2.5)
        if (appt.Receipt == null)
        {
            var receiptAmount = request.Amount ?? appt.Doctor.ConsultationFee;
            var receipt = new Receipt
            {
                AppointmentId = appt.Id,
                ReceiptNumber = ReceiptNumberGenerator.Generate(),
                Amount = receiptAmount,
                PaymentStatus = PaymentStatus.Pending,
                GeneratedAt = DateTime.UtcNow
            };
            _context.Receipts.Add(receipt);
        }

        await _context.SaveChangesAsync();

        // Push in-app notification to patient
        await _notificationService.NotifyUserAsync(
            appt.Patient.UserId,
            "Appointment Approved",
            $"Your dental appointment has been confirmed for {appt.ConfirmedDateTime:dd MMM yyyy, hh:mm tt}. Receipt has been prepared.");

        return Ok(new { message = "Appointment approved successfully.", confirmedDateTime = appt.ConfirmedDateTime, status = appt.Status.ToString() });
    }

    [HttpPut("{id:guid}/reject")]
    [Authorize(Roles = $"{AppRoles.Doctor},{AppRoles.Admin}")]
    public async Task<IActionResult> RejectAppointment(Guid id, [FromBody] RejectAppointmentDto request)
    {
        var role = _currentUserService.Role;
        var doctorId = _currentUserService.DoctorId;

        var appt = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appt == null) return NotFound(new { message = "Appointment not found." });

        if (role == AppRoles.Doctor && (!doctorId.HasValue || appt.DoctorId != doctorId.Value))
            return Forbid();

        appt.Status = AppointmentStatus.Rejected;
        appt.RejectionReason = request.Reason;

        await _context.SaveChangesAsync();

        await _notificationService.NotifyUserAsync(
            appt.Patient.UserId,
            "Appointment Declined",
            $"Your appointment request could not be accepted. Reason: {request.Reason}");

        return Ok(new { message = "Appointment rejected.", status = appt.Status.ToString() });
    }

    [HttpPut("{id:guid}/reschedule")]
    [Authorize(Roles = AppRoles.Doctor)]
    public async Task<IActionResult> RescheduleAppointment(Guid id, [FromBody] RescheduleAppointmentDto request)
    {
        var doctorId = _currentUserService.DoctorId;
        if (!doctorId.HasValue) return Forbid();

        var appt = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appt == null) return NotFound(new { message = "Appointment not found." });
        if (appt.DoctorId != doctorId.Value) return Forbid();

        appt.RequestedDateTime = request.ProposedDateTime;
        appt.ConfirmedDateTime = request.ProposedDateTime;
        appt.Status = AppointmentStatus.Rescheduled;

        await _context.SaveChangesAsync();

        await _notificationService.NotifyUserAsync(
            appt.Patient.UserId,
            "Appointment Rescheduled",
            $"Doctor proposed a new appointment slot: {request.ProposedDateTime:dd MMM yyyy, hh:mm tt}.");

        return Ok(new { message = "Appointment rescheduled.", proposedDateTime = request.ProposedDateTime, status = appt.Status.ToString() });
    }

    [HttpPut("{id:guid}/cancel")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> CancelAppointment(Guid id)
    {
        var patientId = _currentUserService.PatientId;
        if (!patientId.HasValue) return Forbid();

        var appt = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appt == null) return NotFound(new { message = "Appointment not found." });
        if (appt.PatientId != patientId.Value) return Forbid();

        if (appt.Status != AppointmentStatus.Pending)
        {
            return BadRequest(new { message = "Only pending appointments can be cancelled.", code = ErrorCodes.InvalidStatusTransition });
        }

        appt.Status = AppointmentStatus.Cancelled;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Appointment cancelled.", status = appt.Status.ToString() });
    }

    [HttpPost("{id:guid}/diagnosis")]
    [Authorize(Roles = AppRoles.Doctor)]
    public async Task<IActionResult> RecordDiagnosis(Guid id, [FromBody] CreateDiagnosisDto request)
    {
        var doctorId = _currentUserService.DoctorId;
        if (!doctorId.HasValue) return Forbid();

        var appt = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appt == null) return NotFound(new { message = "Appointment not found." });
        if (appt.DoctorId != doctorId.Value) return Forbid();

        var diagnosis = new Diagnosis
        {
            AppointmentId = appt.Id,
            DiseaseName = request.DiseaseName.Trim(),
            Notes = request.Notes,
            Prescription = request.Prescription,
            CreatedAt = DateTime.UtcNow
        };

        _context.Diagnoses.Add(diagnosis);
        appt.Status = AppointmentStatus.Completed;

        await _context.SaveChangesAsync();

        await _notificationService.NotifyUserAsync(
            appt.Patient.UserId,
            "Diagnosis & Prescription Recorded",
            $"Dr. has recorded your clinical notes and prescription for {diagnosis.DiseaseName}. You can view and download your updated receipt.");

        return Ok(new DiagnosisDto
        {
            Id = diagnosis.Id,
            AppointmentId = diagnosis.AppointmentId,
            DiseaseName = diagnosis.DiseaseName,
            Notes = diagnosis.Notes,
            Prescription = diagnosis.Prescription,
            CreatedAt = diagnosis.CreatedAt
        });
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GetAllAppointments([FromQuery] string? status, [FromQuery] string? search)
    {
        var query = _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Receipt)
            .Include(a => a.Diagnoses)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(a => a.Patient.User.FullName.ToLower().Contains(searchLower) ||
                                     a.Doctor.User.FullName.ToLower().Contains(searchLower));
        }

        var list = await query
            .OrderByDescending(a => a.RequestedDateTime)
            .Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                PatientName = a.Patient.User.FullName,
                PatientPhone = a.Patient.User.PhoneNumber,
                PatientEmail = a.Patient.User.Email,
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor.User.FullName,
                DoctorSpecialization = a.Doctor.Specialization,
                RequestedDateTime = a.RequestedDateTime,
                ConfirmedDateTime = a.ConfirmedDateTime,
                Status = a.Status,
                ReasonForVisit = a.ReasonForVisit,
                RejectionReason = a.RejectionReason,
                ConsultationFee = a.Doctor.ConsultationFee,
                ReceiptId = a.Receipt != null ? a.Receipt.Id : null,
                PaymentStatus = a.Receipt != null ? a.Receipt.PaymentStatus : null,
                Diagnoses = a.Diagnoses.Select(d => new DiagnosisDto
                {
                    Id = d.Id,
                    AppointmentId = d.AppointmentId,
                    DiseaseName = d.DiseaseName,
                    Notes = d.Notes,
                    Prescription = d.Prescription,
                    CreatedAt = d.CreatedAt
                }).ToList(),
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }
}
