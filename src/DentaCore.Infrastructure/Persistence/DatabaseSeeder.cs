using DentaCore.Application.Interfaces;
using DentaCore.Domain.Entities;
using DentaCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentaCore.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // 1. Seed Admin
        var adminEmail = _configuration["ADMIN_EMAIL"] ?? "admin@dentacore.local";
        var adminUser = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (adminUser == null)
        {
            _logger.LogInformation("Seeding default Administrator account: {Email}", adminEmail);
            var adminPassword = _configuration["ADMIN_PASSWORD"] ?? "Admin@12345!";
            var adminName = _configuration["ADMIN_FULLNAME"] ?? "System Administrator";
            var adminPhone = _configuration["ADMIN_PHONE"] ?? "+1234567890";

            adminUser = new User
            {
                FullName = adminName,
                Email = adminEmail,
                PhoneNumber = adminPhone,
                PasswordHash = _passwordHasher.HashPassword(adminPassword),
                Role = UserRole.Admin,
                EmailConfirmed = true,
                IsActive = true
            };

            _context.Users.Add(adminUser);

            var adminProfile = new Admin
            {
                UserId = adminUser.Id,
                Designation = "Head Practice Administrator"
            };
            _context.Admins.Add(adminProfile);
        }

        // 2. Seed Doctor
        var doctorEmail = _configuration["DOCTOR_EMAIL"] ?? "doctor@dentacore.local";
        var doctorUser = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == doctorEmail);

        if (doctorUser == null)
        {
            _logger.LogInformation("Seeding default Doctor account: {Email}", doctorEmail);
            var docPassword = _configuration["DOCTOR_PASSWORD"] ?? "Doctor@12345!";
            var docName = _configuration["DOCTOR_FULLNAME"] ?? "Dr. Amara Sterling";
            var docPhone = _configuration["DOCTOR_PHONE"] ?? "+1987654321";
            var docSpec = _configuration["DOCTOR_SPECIALIZATION"] ?? "Endodontics & Restorative";
            var docLicense = _configuration["DOCTOR_LICENSE"] ?? "DDS-84920";
            var feeStr = _configuration["DOCTOR_FEE"];
            var fee = decimal.TryParse(feeStr, out var f) ? f : 180.00m;

            doctorUser = new User
            {
                FullName = docName,
                Email = doctorEmail,
                PhoneNumber = docPhone,
                PasswordHash = _passwordHasher.HashPassword(docPassword),
                Role = UserRole.Doctor,
                EmailConfirmed = true,
                IsActive = true
            };

            _context.Users.Add(doctorUser);

            var doctorProfile = new Doctor
            {
                UserId = doctorUser.Id,
                Specialization = docSpec,
                LicenseNumber = docLicense,
                ExperienceYears = 12,
                Bio = "Specialist in endodontics, root canals, and cosmetic dental rehabilitation with 12+ years of clinical excellence.",
                ConsultationFee = fee,
                IsApprovedByAdmin = true
            };
            _context.Doctors.Add(doctorProfile);

            // Default weekly schedule (Monday to Friday, 09:00 - 17:00, 30 min slots)
            var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
            foreach (var day in days)
            {
                _context.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = doctorProfile.Id,
                    DayOfWeek = day,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(17, 0),
                    SlotDurationMinutes = 30,
                    IsActive = true
                });
            }
        }

        // 3. Seed Demo Patient
        var patientEmail = "patient@dentacore.local";
        var patientUser = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == patientEmail);

        if (patientUser == null)
        {
            _logger.LogInformation("Seeding default Demo Patient account: {Email}", patientEmail);
            patientUser = new User
            {
                FullName = "Jane Doe",
                Email = patientEmail,
                PhoneNumber = "+15552345678",
                PasswordHash = _passwordHasher.HashPassword("Patient@12345!"),
                Role = UserRole.Patient,
                EmailConfirmed = true,
                IsActive = true
            };

            _context.Users.Add(patientUser);

            var patientProfile = new Patient
            {
                UserId = patientUser.Id,
                DateOfBirth = new DateOnly(1994, 5, 14),
                Gender = "Female",
                Address = "742 Evergreen Terrace, Springfield",
                BloodGroup = "O+",
                MedicalHistory = "No known drug allergies. Mild seasonal asthma.",
                EmergencyContact = "John Doe (+15552349999)"
            };
            _context.Patients.Add(patientProfile);
        }

        await _context.SaveChangesAsync();
    }
}
