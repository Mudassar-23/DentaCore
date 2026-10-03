using DentaCore.Application.Common;
using DentaCore.Application.DTOs;
using DentaCore.Application.Interfaces;
using DentaCore.Domain.Entities;
using DentaCore.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentaCore.API.Controllers;

[ApiController]
[Route("api/v1/patients")]
public class PatientsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public PatientsController(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> GetMyProfile()
    {
        var patientId = _currentUserService.PatientId;
        var userId = _currentUserService.UserId;

        Patient? patient = null;
        if (patientId.HasValue)
        {
            patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId.Value);
        }

        if (patient == null && userId.HasValue)
        {
            patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);
        }

        // Auto-heal: If user has Patient role but Patient record was missing, create it
        if (patient == null && userId.HasValue)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (user != null)
            {
                patient = new Patient
                {
                    UserId = user.Id,
                    User = user
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
            }
        }

        if (patient == null)
            return NotFound(new { message = "Patient profile not found." });

        return Ok(new PatientDto
        {
            Id = patient.Id,
            UserId = patient.UserId,
            FullName = patient.User.FullName,
            Email = patient.User.Email,
            PhoneNumber = patient.User.PhoneNumber,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            Address = patient.Address,
            BloodGroup = patient.BloodGroup,
            MedicalHistory = patient.MedicalHistory,
            EmergencyContact = patient.EmergencyContact,
            IsActive = patient.User.IsActive
        });
    }

    [HttpPut("me")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdatePatientDto request)
    {
        var patientId = _currentUserService.PatientId;
        var userId = _currentUserService.UserId;

        Patient? patient = null;
        if (patientId.HasValue)
        {
            patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId.Value);
        }

        if (patient == null && userId.HasValue)
        {
            patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);
        }

        if (patient == null)
            return NotFound(new { message = "Patient profile not found." });

        if (!string.IsNullOrWhiteSpace(request.FullName))
            patient.User.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            patient.User.PhoneNumber = request.PhoneNumber.Trim();

        patient.DateOfBirth = request.DateOfBirth ?? patient.DateOfBirth;
        patient.Gender = request.Gender ?? patient.Gender;
        patient.Address = request.Address ?? patient.Address;
        patient.BloodGroup = request.BloodGroup ?? patient.BloodGroup;
        patient.MedicalHistory = request.MedicalHistory ?? patient.MedicalHistory;
        patient.EmergencyContact = request.EmergencyContact ?? patient.EmergencyContact;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Profile updated successfully." });
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Doctor}")]
    public async Task<IActionResult> GetPatients([FromQuery] string? search)
    {
        var query = _context.Patients
            .Include(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(p => p.User.FullName.ToLower().Contains(searchLower) ||
                                     p.User.Email.ToLower().Contains(searchLower) ||
                                     p.User.PhoneNumber.Contains(searchLower));
        }

        var list = await query
            .Select(p => new PatientDto
            {
                Id = p.Id,
                UserId = p.UserId,
                FullName = p.User.FullName,
                Email = p.User.Email,
                PhoneNumber = p.User.PhoneNumber,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                Address = p.Address,
                BloodGroup = p.BloodGroup,
                MedicalHistory = p.MedicalHistory,
                EmergencyContact = p.EmergencyContact,
                IsActive = p.User.IsActive
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Doctor}")]
    public async Task<IActionResult> GetPatientById(Guid id)
    {
        var patient = await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (patient == null) return NotFound(new { message = "Patient not found." });

        return Ok(new PatientDto
        {
            Id = patient.Id,
            UserId = patient.UserId,
            FullName = patient.User.FullName,
            Email = patient.User.Email,
            PhoneNumber = patient.User.PhoneNumber,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            Address = patient.Address,
            BloodGroup = patient.BloodGroup,
            MedicalHistory = patient.MedicalHistory,
            EmergencyContact = patient.EmergencyContact,
            IsActive = patient.User.IsActive
        });
    }
}
