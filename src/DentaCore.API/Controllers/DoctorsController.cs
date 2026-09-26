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
[Route("api/v1/doctors")]
public class DoctorsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DoctorsController(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetDoctors([FromQuery] string? specialization)
    {
        var query = _context.Doctors
            .Include(d => d.User)
            .Where(d => d.IsApprovedByAdmin && d.User.IsActive);

        if (!string.IsNullOrWhiteSpace(specialization))
        {
            var specLower = specialization.Trim().ToLower();
            query = query.Where(d => d.Specialization.ToLower().Contains(specLower));
        }

        var doctors = await query
            .Select(d => new DoctorDto
            {
                Id = d.Id,
                UserId = d.UserId,
                FullName = d.User.FullName,
                Email = d.User.Email,
                PhoneNumber = d.User.PhoneNumber,
                Specialization = d.Specialization,
                LicenseNumber = d.LicenseNumber,
                ExperienceYears = d.ExperienceYears,
                Bio = d.Bio,
                ConsultationFee = d.ConsultationFee,
                IsApprovedByAdmin = d.IsApprovedByAdmin,
                IsActive = d.User.IsActive
            })
            .ToListAsync();

        return Ok(doctors);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDoctor(Guid id)
    {
        var doctor = await _context.Doctors
            .Include(d => d.User)
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doctor == null) return NotFound(new { message = "Doctor not found." });

        return Ok(new DoctorDto
        {
            Id = doctor.Id,
            UserId = doctor.UserId,
            FullName = doctor.User.FullName,
            Email = doctor.User.Email,
            PhoneNumber = doctor.User.PhoneNumber,
            Specialization = doctor.Specialization,
            LicenseNumber = doctor.LicenseNumber,
            ExperienceYears = doctor.ExperienceYears,
            Bio = doctor.Bio,
            ConsultationFee = doctor.ConsultationFee,
            IsApprovedByAdmin = doctor.IsApprovedByAdmin,
            IsActive = doctor.User.IsActive
        });
    }

    [HttpGet("{id:guid}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability(Guid id, [FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var targetDate))
        {
            return BadRequest(new { message = "Invalid date format. Use YYYY-MM-DD." });
        }

        var doctor = await _context.Doctors
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doctor == null) return NotFound(new { message = "Doctor not found." });

        var dayName = targetDate.DayOfWeek.ToString();
        var schedule = doctor.Schedules.FirstOrDefault(s => s.DayOfWeek.Equals(dayName, StringComparison.OrdinalIgnoreCase) && s.IsActive);

        if (schedule == null)
        {
            return Ok(Array.Empty<AvailableSlotDto>());
        }

        // Query existing appointments on that date for this doctor (excluding cancelled/rejected)
        var startOfDay = targetDate.ToDateTime(TimeOnly.MinValue);
        var endOfDay = targetDate.ToDateTime(TimeOnly.MaxValue);

        var existingAppointments = await _context.Appointments
            .Where(a => a.DoctorId == id &&
                        a.Status != AppointmentStatus.Cancelled &&
                        a.Status != AppointmentStatus.Rejected &&
                        ((a.ConfirmedDateTime.HasValue && a.ConfirmedDateTime.Value >= startOfDay && a.ConfirmedDateTime.Value <= endOfDay) ||
                         (a.RequestedDateTime >= startOfDay && a.RequestedDateTime <= endOfDay)))
            .ToListAsync();

        var slots = new List<AvailableSlotDto>();
        var duration = schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : 30;
        var currentSlotTime = schedule.StartTime;

        while (currentSlotTime.AddMinutes(duration) <= schedule.EndTime)
        {
            var slotStart = targetDate.ToDateTime(currentSlotTime);
            var slotEnd = slotStart.AddMinutes(duration);

            bool isBooked = existingAppointments.Any(a =>
            {
                var apptTime = a.ConfirmedDateTime ?? a.RequestedDateTime;
                return Math.Abs((apptTime - slotStart).TotalMinutes) < duration;
            });

            // If slot is in the past (e.g. today's earlier hours)
            if (slotStart <= DateTime.UtcNow)
            {
                isBooked = true;
            }

            slots.Add(new AvailableSlotDto
            {
                StartTime = slotStart,
                EndTime = slotEnd,
                IsAvailable = !isBooked
            });

            currentSlotTime = currentSlotTime.AddMinutes(duration);
        }

        return Ok(slots);
    }

    [HttpGet("me/schedule")]
    [Authorize(Roles = AppRoles.Doctor)]
    public async Task<IActionResult> GetMySchedule()
    {
        var doctorId = _currentUserService.DoctorId;
        if (!doctorId.HasValue) return Forbid();

        var schedules = await _context.DoctorSchedules
            .Where(s => s.DoctorId == doctorId.Value)
            .Select(s => new DoctorScheduleDto
            {
                Id = s.Id,
                DoctorId = s.DoctorId,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime.ToString("HH:mm"),
                EndTime = s.EndTime.ToString("HH:mm"),
                SlotDurationMinutes = s.SlotDurationMinutes,
                IsActive = s.IsActive
            })
            .ToListAsync();

        return Ok(schedules);
    }

    [HttpPut("me/schedule")]
    [Authorize(Roles = AppRoles.Doctor)]
    public async Task<IActionResult> UpdateMySchedule([FromBody] UpdateDoctorScheduleRequest request)
    {
        var doctorId = _currentUserService.DoctorId;
        if (!doctorId.HasValue) return Forbid();

        var existingSchedules = await _context.DoctorSchedules
            .Where(s => s.DoctorId == doctorId.Value)
            .ToListAsync();

        _context.DoctorSchedules.RemoveRange(existingSchedules);

        foreach (var item in request.Schedules)
        {
            if (TimeOnly.TryParse(item.StartTime, out var start) && TimeOnly.TryParse(item.EndTime, out var end))
            {
                _context.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = doctorId.Value,
                    DayOfWeek = item.DayOfWeek,
                    StartTime = start,
                    EndTime = end,
                    SlotDurationMinutes = item.SlotDurationMinutes > 0 ? item.SlotDurationMinutes : 30,
                    IsActive = item.IsActive
                });
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Schedule updated successfully." });
    }

    [HttpGet("me/patients")]
    [Authorize(Roles = AppRoles.Doctor)]
    public async Task<IActionResult> GetMyPatients()
    {
        var doctorId = _currentUserService.DoctorId;
        if (!doctorId.HasValue) return Forbid();

        var appointments = await _context.Appointments
            .Include(a => a.Patient)
                .ThenInclude(p => p.User)
            .Include(a => a.Diagnoses)
            .Where(a => a.DoctorId == doctorId.Value)
            .OrderByDescending(a => a.RequestedDateTime)
            .ToListAsync();

        var patientsGrouped = appointments
            .GroupBy(a => a.PatientId)
            .Select(g =>
            {
                var patient = g.First().Patient;
                return new
                {
                    patientId = patient.Id,
                    userId = patient.UserId,
                    fullName = patient.User.FullName,
                    email = patient.User.Email,
                    phoneNumber = patient.User.PhoneNumber,
                    bloodGroup = patient.BloodGroup,
                    dateOfBirth = patient.DateOfBirth,
                    gender = patient.Gender,
                    medicalHistory = patient.MedicalHistory,
                    emergencyContact = patient.EmergencyContact,
                    totalVisits = g.Count(),
                    lastVisit = g.Max(a => a.ConfirmedDateTime ?? a.RequestedDateTime),
                    diagnoses = g.SelectMany(a => a.Diagnoses.Select(d => d.DiseaseName)).Distinct().ToList()
                };
            })
            .ToList();

        return Ok(patientsGrouped);
    }
}
