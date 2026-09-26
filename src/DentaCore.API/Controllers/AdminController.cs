using DentaCore.Application.DTOs;
using DentaCore.Application.Interfaces;
using DentaCore.Domain.Enums;
using DentaCore.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentaCore.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AdminController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var totalPatients = await _context.Patients.CountAsync();
        var totalDoctors = await _context.Doctors.CountAsync(d => d.IsApprovedByAdmin);
        var pendingDoctors = await _context.Doctors.CountAsync(d => !d.IsApprovedByAdmin);
        var totalAppointments = await _context.Appointments.CountAsync();
        var pendingAppointments = await _context.Appointments.CountAsync(a => a.Status == AppointmentStatus.Pending);
        var completedAppointments = await _context.Appointments.CountAsync(a => a.Status == AppointmentStatus.Completed);

        var receipts = await _context.Receipts.ToListAsync();
        var totalRevenue = receipts.Where(r => r.PaymentStatus == PaymentStatus.Paid).Sum(r => r.Amount);
        var pendingRevenue = receipts.Where(r => r.PaymentStatus == PaymentStatus.Pending).Sum(r => r.Amount);

        return Ok(new
        {
            totalPatients,
            totalDoctors,
            pendingDoctors,
            totalAppointments,
            pendingAppointments,
            completedAppointments,
            totalRevenue,
            pendingRevenue
        });
    }

    [HttpGet("doctors")]
    public async Task<IActionResult> GetAllDoctors()
    {
        var doctors = await _context.Doctors
            .Include(d => d.User)
            .OrderByDescending(d => d.CreatedAt)
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

    [HttpPost("doctors/{id:guid}/approve")]
    public async Task<IActionResult> ApproveDoctor(Guid id)
    {
        var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor == null) return NotFound(new { message = "Doctor not found." });

        doctor.IsApprovedByAdmin = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Dr. {doctor.User.FullName} has been approved." });
    }

    [HttpPut("doctors/{id:guid}/status")]
    public async Task<IActionResult> ToggleDoctorStatus(Guid id, [FromQuery] bool active)
    {
        var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor == null) return NotFound(new { message = "Doctor not found." });

        doctor.User.IsActive = active;
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Dr. {doctor.User.FullName} active status set to {active}." });
    }
}
