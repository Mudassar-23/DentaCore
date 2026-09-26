using DentaCore.Application.Interfaces;
using DentaCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentaCore.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---- User ----
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(150).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            entity.Property(u => u.PhoneNumber).HasMaxLength(30);
            entity.HasQueryFilter(u => !u.IsDeleted);

            entity.HasOne(u => u.Patient)
                .WithOne(p => p.User)
                .HasForeignKey<Patient>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(u => u.Doctor)
                .WithOne(d => d.User)
                .HasForeignKey<Doctor>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(u => u.Admin)
                .WithOne(a => a.User)
                .HasForeignKey<Admin>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.RefreshTokens)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Notifications)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Doctor ----
        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.ConsultationFee).HasPrecision(18, 2);
            entity.Property(d => d.Specialization).HasMaxLength(100);
            entity.Property(d => d.LicenseNumber).HasMaxLength(50);

            entity.HasMany(d => d.Schedules)
                .WithOne(s => s.Doctor)
                .HasForeignKey(s => s.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Appointment ----
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasQueryFilter(a => !a.IsDeleted);

            entity.HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Receipt)
                .WithOne(r => r.Appointment)
                .HasForeignKey<Receipt>(r => r.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(a => a.Diagnoses)
                .WithOne(d => d.Appointment)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => new { a.DoctorId, a.ConfirmedDateTime });
            entity.HasIndex(a => new { a.DoctorId, a.Status });
            entity.HasIndex(a => new { a.PatientId, a.Status });
        });

        // ---- Receipt ----
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.ReceiptNumber).IsUnique();
            entity.Property(r => r.ReceiptNumber).HasMaxLength(50).IsRequired();
            entity.Property(r => r.Amount).HasPrecision(18, 2);

            entity.HasOne(r => r.Payment)
                .WithOne(p => p.Receipt)
                .HasForeignKey<Payment>(p => p.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Payment ----
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.AmountPaid).HasPrecision(18, 2);
            entity.Property(p => p.TransactionRef).HasMaxLength(100);
        });

        // ---- DoctorSchedule ----
        modelBuilder.Entity<DoctorSchedule>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.DayOfWeek).HasMaxLength(20);
        });

        // ---- AuditLog ----
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(al => al.Id);
            entity.Property(al => al.Action).HasMaxLength(50);
            entity.Property(al => al.EntityName).HasMaxLength(100);
            entity.HasIndex(al => al.CreatedAt);
            entity.HasIndex(al => al.UserId);
        });

        // ---- Notification ----
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Title).HasMaxLength(150);
            entity.HasIndex(n => new { n.UserId, n.IsRead });
        });
    }
}
