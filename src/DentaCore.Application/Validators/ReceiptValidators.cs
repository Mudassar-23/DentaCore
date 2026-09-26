using DentaCore.Application.DTOs;
using FluentValidation;

namespace DentaCore.Application.Validators;

public class MarkPaymentValidator : AbstractValidator<MarkPaymentDto>
{
    public MarkPaymentValidator()
    {
        RuleFor(x => x.AmountPaid).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).IsInEnum();
    }
}
