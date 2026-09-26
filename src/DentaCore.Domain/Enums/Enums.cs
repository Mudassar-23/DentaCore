namespace DentaCore.Domain.Enums;

public enum UserRole
{
    Patient = 0,
    Doctor = 1,
    Admin = 2
}

public enum AppointmentStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Rescheduled = 3,
    Completed = 4,
    Cancelled = 5
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Refunded = 2
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Online = 2
}
