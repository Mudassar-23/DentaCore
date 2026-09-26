using DentaCore.Application.DTOs;
using FluentValidation;

namespace DentaCore.Application.Validators;

public class CreateAppointmentValidator : AbstractValidator<CreateAppointmentDto>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.RequestedDateTime)
            .NotEmpty()
            .GreaterThan(DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Appointment requested date time must be in the future.");
    }
}

public class ApproveAppointmentValidator : AbstractValidator<ApproveAppointmentDto>
{
    public ApproveAppointmentValidator()
    {
        RuleFor(x => x.ConfirmedDateTime).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).When(x => x.Amount.HasValue);
    }
}

public class RejectAppointmentValidator : AbstractValidator<RejectAppointmentDto>
{
    public RejectAppointmentValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class RescheduleAppointmentValidator : AbstractValidator<RescheduleAppointmentDto>
{
    public RescheduleAppointmentValidator()
    {
        RuleFor(x => x.ProposedDateTime).NotEmpty().GreaterThan(DateTime.UtcNow);
    }
}
