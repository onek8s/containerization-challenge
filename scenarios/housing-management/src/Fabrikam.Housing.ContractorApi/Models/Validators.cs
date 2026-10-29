using System;
using FluentValidation;
using Fabrikam.Housing.Compliance;

namespace Fabrikam.Housing.ContractorApi.Models
{
    public class RejectRequestValidator : AbstractValidator<RejectRequest>
    {
        public RejectRequestValidator()
        {
            RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        }
    }

    public class AppointmentRequestValidator : AbstractValidator<AppointmentRequest>
    {
        public AppointmentRequestValidator()
        {
            RuleFor(x => x.Date).NotEmpty();
            RuleFor(x => x.Slot).IsInEnum();
        }
    }

    public class NoAccessRequestValidator : AbstractValidator<NoAccessRequest>
    {
        public NoAccessRequestValidator()
        {
            RuleFor(x => x.Outcome).NotEmpty().MaximumLength(200);
        }
    }

    public class CompleteRequestValidator : AbstractValidator<CompleteRequest>
    {
        public CompleteRequestValidator()
        {
            RuleFor(x => x.Notes).NotEmpty().MaximumLength(2000);
            RuleFor(x => x.LabourHours).InclusiveBetween(0m, 80m);
            RuleForEach(x => x.ExtraLines).ChildRules(line =>
            {
                line.RuleFor(l => l.SorCode).NotEmpty().MaximumLength(10);
                line.RuleFor(l => l.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
            });
            RuleFor(x => x.RechargeReason).NotEmpty().When(x => x.SuggestRecharge)
                .WithMessage("Give a reason when suggesting a recharge to the tenant.");
        }
    }

    public class InvoiceRequestValidator : AbstractValidator<InvoiceRequest>
    {
        public InvoiceRequestValidator()
        {
            RuleFor(x => x.InvoiceNumber).NotEmpty().MaximumLength(30);
            RuleFor(x => x.InvoiceDate).NotEmpty().LessThanOrEqualTo(_ => DateTime.Today.AddDays(1));
            RuleFor(x => x.WorkOrderReference).NotEmpty().MaximumLength(12);
            RuleFor(x => x.NetAmount).GreaterThan(0).LessThan(100000);
            RuleFor(x => x.VatAmount).GreaterThanOrEqualTo(0);
        }
    }

    public class CertificateRequestValidator : AbstractValidator<CertificateRequest>
    {
        public CertificateRequestValidator()
        {
            RuleFor(x => x.PropertyReference).NotEmpty();
            RuleFor(x => x.Type).Must(t => t == ComplianceType.GasSafety || t == ComplianceType.Electrical)
                .WithMessage("Only gas safety and electrical certificates can be uploaded through the API.");
            RuleFor(x => x.CertificateNumber).NotEmpty().MaximumLength(40);
            RuleFor(x => x.InspectedOn).NotEmpty().LessThanOrEqualTo(_ => DateTime.Today.AddDays(1));
            RuleFor(x => x.Outcome).IsInEnum();
            RuleFor(x => x.EngineerName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.EngineerRegistration).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Observations).NotEmpty().When(x => x.Outcome != CertificateOutcome.Satisfactory)
                .WithMessage("Observations are required when the outcome is not satisfactory.");
            RuleFor(x => x.File).NotNull().WithMessage("Attach the certificate as a PDF.");
            RuleFor(x => x.File.ContentType).Equal("application/pdf").When(x => x.File != null)
                .WithMessage("The certificate must be a PDF.");
            RuleFor(x => x.File.Length).LessThanOrEqualTo(10 * 1024 * 1024).When(x => x.File != null);
        }
    }
}
