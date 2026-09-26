using DentaCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentaCore.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Patient> Patients { get; }
    DbSet<Doctor> Doctors { get; }
    DbSet<Admin> Admins { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Diagnosis> Diagnoses { get; }
    DbSet<Receipt> Receipts { get; }
    DbSet<Payment> Payments { get; }
    DbSet<DoctorSchedule> DoctorSchedules { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
