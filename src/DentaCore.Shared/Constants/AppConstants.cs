namespace DentaCore.Shared.Constants;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Doctor = "Doctor";
    public const string Patient = "Patient";
}

public static class PolicyNames
{
    public const string DoctorOwnsAppointment = "DoctorOwnsAppointment";
    public const string PatientOwnsAppointment = "PatientOwnsAppointment";
    public const string DoctorOwnsReceipt = "DoctorOwnsReceipt";
    public const string PatientOwnsReceipt = "PatientOwnsReceipt";
}

public static class ErrorCodes
{
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string EmailNotConfirmed = "EMAIL_NOT_CONFIRMED";
    public const string AccountDeactivated = "ACCOUNT_DEACTIVATED";
    public const string DuplicateEmail = "DUPLICATE_EMAIL";
    public const string AppointmentNotFound = "APPOINTMENT_NOT_FOUND";
    public const string SlotUnavailable = "SLOT_UNAVAILABLE";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string ReceiptNotFound = "RECEIPT_NOT_FOUND";
    public const string DoctorNotApproved = "DOCTOR_NOT_APPROVED";
}
