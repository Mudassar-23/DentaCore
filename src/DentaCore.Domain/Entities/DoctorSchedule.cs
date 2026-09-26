using DentaCore.Domain.Common;

namespace DentaCore.Domain.Entities;

/// <summary>
/// Doctor's recurring weekly availability slots.
/// </summary>
public class DoctorSchedule : BaseEntity
{
    public Guid DoctorId { get; set; }
    public string DayOfWeek { get; set; } = string.Empty; // e.g. "Monday"
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int SlotDurationMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;

    // Navigation
    public Doctor Doctor { get; set; } = null!;
}
