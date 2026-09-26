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
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _currentUserService = currentUserService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var user = await _context.Users
            .Include(u => u.Patient)
            .Include(u => u.Doctor)
            .Include(u => u.Admin)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower());

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password.", code = ErrorCodes.InvalidCredentials });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Your account has been deactivated.", code = ErrorCodes.AccountDeactivated });
        }

        Guid? profileId = user.Role switch
        {
            UserRole.Patient => user.Patient?.Id,
            UserRole.Doctor => user.Doctor?.Id,
            UserRole.Admin => user.Admin?.Id,
            _ => null
        };

        var (accessToken, refreshToken, accessExpiresAt, refreshExpiresAt) = _jwtTokenService.GenerateTokens(user, profileId);

        // Store refresh token
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = refreshExpiresAt,
            IsRevoked = false
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        var response = new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessExpiresAt = accessExpiresAt,
            User = new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role.ToString(),
                ProfileId = profileId
            }
        };

        return Ok(response);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterPatient([FromBody] RegisterPatientDto request)
    {
        var existing = await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower());
        if (existing)
        {
            return BadRequest(new { message = "An account with this email already exists.", code = ErrorCodes.DuplicateEmail });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLower(),
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Patient,
            EmailConfirmed = true, // Auto-confirmed for frictionless demo/clinical setup
            IsActive = true
        };

        _context.Users.Add(user);

        var patient = new Patient
        {
            UserId = user.Id,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address,
            BloodGroup = request.BloodGroup,
            MedicalHistory = request.MedicalHistory,
            EmergencyContact = request.EmergencyContact
        };

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var (accessToken, refreshToken, accessExpiresAt, refreshExpiresAt) = _jwtTokenService.GenerateTokens(user, patient.Id);

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = refreshExpiresAt,
            IsRevoked = false
        });
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMe), new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessExpiresAt = accessExpiresAt,
            User = new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role.ToString(),
                ProfileId = patient.Id
            }
        });
    }

    [HttpPost("register-doctor")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterDoctor([FromBody] RegisterDoctorDto request)
    {
        var existing = await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower());
        if (existing)
        {
            return BadRequest(new { message = "An account with this email already exists.", code = ErrorCodes.DuplicateEmail });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLower(),
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Doctor,
            EmailConfirmed = true,
            IsActive = true
        };

        _context.Users.Add(user);

        var doctor = new Doctor
        {
            UserId = user.Id,
            Specialization = request.Specialization.Trim(),
            LicenseNumber = request.LicenseNumber.Trim(),
            ExperienceYears = request.ExperienceYears,
            Bio = request.Bio,
            ConsultationFee = request.ConsultationFee,
            IsApprovedByAdmin = false // Requires Admin approval
        };

        _context.Doctors.Add(doctor);

        // Prepopulate standard weekly schedule (Mon-Fri)
        var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
        foreach (var day in days)
        {
            _context.DoctorSchedules.Add(new DoctorSchedule
            {
                DoctorId = doctor.Id,
                DayOfWeek = day,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                SlotDurationMinutes = 30,
                IsActive = true
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Doctor registration submitted successfully. Your account is pending administrator approval.",
            doctorId = doctor.Id,
            isApproved = false
        });
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        var storedToken = await _context.RefreshTokens
            .Include(r => r.User)
                .ThenInclude(u => u.Patient)
            .Include(r => r.User)
                .ThenInclude(u => u.Doctor)
            .Include(r => r.User)
                .ThenInclude(u => u.Admin)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken);

        if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        // Revoke the old token (rotation)
        storedToken.IsRevoked = true;

        var user = storedToken.User;
        Guid? profileId = user.Role switch
        {
            UserRole.Patient => user.Patient?.Id,
            UserRole.Doctor => user.Doctor?.Id,
            UserRole.Admin => user.Admin?.Id,
            _ => null
        };

        var (accessToken, newRefreshToken, accessExpiresAt, refreshExpiresAt) = _jwtTokenService.GenerateTokens(user, profileId);

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = refreshExpiresAt,
            IsRevoked = false
        });

        await _context.SaveChangesAsync();

        return Ok(new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            AccessExpiresAt = accessExpiresAt,
            User = new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role.ToString(),
                ProfileId = profileId
            }
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto? request)
    {
        if (request != null && !string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == request.RefreshToken);
            if (storedToken != null)
            {
                storedToken.IsRevoked = true;
                await _context.SaveChangesAsync();
            }
        }

        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var user = await _context.Users
            .Include(u => u.Patient)
            .Include(u => u.Doctor)
            .Include(u => u.Admin)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null) return NotFound();

        Guid? profileId = user.Role switch
        {
            UserRole.Patient => user.Patient?.Id,
            UserRole.Doctor => user.Doctor?.Id,
            UserRole.Admin => user.Admin?.Id,
            _ => null
        };

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            ProfileId = profileId
        });
    }
}
