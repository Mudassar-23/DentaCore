using DentaCore.Application.DTOs;
using FluentValidation;

namespace DentaCore.Application.Validators;

public class CreateDiagnosisValidator : AbstractValidator<CreateDiagnosisDto>
{
    public CreateDiagnosisValidator()
    {
        RuleFor(x => x.DiseaseName).NotEmpty().MaximumLength(200);
    }
}
