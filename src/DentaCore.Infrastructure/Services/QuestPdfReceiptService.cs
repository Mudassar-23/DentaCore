using DentaCore.Application.Interfaces;
using DentaCore.Domain.Entities;
using DentaCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DentaCore.Infrastructure.Services;

public class QuestPdfReceiptService : IPdfReceiptService
{
    private readonly IApplicationDbContext _context;

    public QuestPdfReceiptService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> GenerateReceiptPdfBytesAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Payment)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Doctor)
                    .ThenInclude(d => d.User)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Diagnoses)
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

        if (receipt == null)
            throw new InvalidOperationException($"Receipt with ID {receiptId} was not found.");

        var appt = receipt.Appointment;
        var patientUser = appt.Patient.User;
        var doctorUser = appt.Doctor.User;
        var doctor = appt.Doctor;
        var diagnosis = appt.Diagnoses.OrderByDescending(d => d.CreatedAt).FirstOrDefault();
        var payment = receipt.Payment;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor("#262624"));

                // Header
                page.Header().Element(headerContainer =>
                {
                    headerContainer.BorderBottom(1).BorderColor("#1F8A55").PaddingBottom(12).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("DentaCore Dental Clinic").FontSize(20).Bold().FontColor("#14532D");
                            col.Item().Text("100 Healthcare Ave, Suite 400 | +1 (555) 019-2831").FontSize(9).FontColor("#666666");
                            col.Item().Text("contact@dentacore.local | www.dentacore.local").FontSize(9).FontColor("#666666");
                        });

                        row.RelativeItem().Column(col =>
                        {
                            col.Item().AlignRight().Text($"RECEIPT: {receipt.ReceiptNumber}").FontSize(12).Bold().FontColor("#14532D");
                            col.Item().AlignRight().Text($"Issued: {receipt.GeneratedAt:dd MMM yyyy, hh:mm tt}").FontSize(9).FontColor("#666666");
                            if (appt.ConfirmedDateTime.HasValue)
                            {
                                col.Item().AlignRight().Text($"Visit: {appt.ConfirmedDateTime:dd MMM yyyy, hh:mm tt}").FontSize(9).FontColor("#666666");
                            }
                        });
                    });
                });

                // Content
                page.Content().PaddingVertical(16).Column(col =>
                {
                    // Doctor and Patient cards
                    col.Item().Row(r =>
                    {
                        // Patient Card
                        r.RelativeItem().Border(1).BorderColor("#e5e5e0").Background("#fafaf8").Padding(12).Column(c =>
                        {
                            c.Item().Text("PATIENT DETAILS").FontSize(9).Bold().FontColor("#1F8A55");
                            c.Item().Text(patientUser.FullName).FontSize(13).Bold();
                            c.Item().Text($"Email: {patientUser.Email}").FontSize(9);
                            c.Item().Text($"Phone: {patientUser.PhoneNumber}").FontSize(9);
                            if (appt.Patient.BloodGroup != null)
                                c.Item().Text($"Blood Group: {appt.Patient.BloodGroup}").FontSize(9);
                        });

                        r.ConstantItem(16);

                        // Doctor Card
                        r.RelativeItem().Border(1).BorderColor("#e5e5e0").Background("#fafaf8").Padding(12).Column(c =>
                        {
                            c.Item().Text("ATTENDING DOCTOR").FontSize(9).Bold().FontColor("#1F8A55");
                            c.Item().Text($"Dr. {doctorUser.FullName}").FontSize(13).Bold();
                            c.Item().Text($"Specialization: {doctor.Specialization}").FontSize(9);
                            c.Item().Text($"License: {doctor.LicenseNumber}").FontSize(9);
                            c.Item().Text($"Contact: {doctorUser.Email}").FontSize(9);
                        });
                    });

                    col.Item().PaddingTop(16);

                    // Clinical Diagnosis Section (if recorded)
                    if (diagnosis != null)
                    {
                        col.Item().Border(1).BorderColor("#cbd5e1").Background("#f8fafc").Padding(12).Column(c =>
                        {
                            c.Item().Text("CLINICAL DIAGNOSIS & TREATMENT NOTES").FontSize(9).Bold().FontColor("#0f172a");
                            c.Item().PaddingTop(4).Text($"Condition / Disease: {diagnosis.DiseaseName}").Bold().FontSize(11);
                            if (!string.IsNullOrWhiteSpace(diagnosis.Notes))
                                c.Item().PaddingTop(2).Text($"Notes: {diagnosis.Notes}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(diagnosis.Prescription))
                                c.Item().PaddingTop(2).Text($"Prescription: {diagnosis.Prescription}").FontSize(9).Italic();
                        });

                        col.Item().PaddingTop(16);
                    }

                    // Line Items Table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background("#14532D").Padding(8).Text("SERVICE / TREATMENT DESCRIPTION").FontColor(Colors.White).Bold().FontSize(9);
                            h.Cell().Background("#14532D").Padding(8).AlignRight().Text("AMOUNT").FontColor(Colors.White).Bold().FontSize(9);
                        });

                        var serviceDescription = !string.IsNullOrWhiteSpace(diagnosis?.DiseaseName)
                            ? $"Dental Consultation & Treatment: {diagnosis.DiseaseName}"
                            : "Dental Consultation & Comprehensive Oral Examination";

                        table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(10).Text(serviceDescription).FontSize(10);
                        table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(10).AlignRight().Text($"${receipt.Amount:N2}").FontSize(10).Bold();

                        // Total Row
                        table.Cell().Padding(10).AlignRight().Text("TOTAL DUE:").Bold().FontSize(12);
                        table.Cell().Padding(10).AlignRight().Text($"${receipt.Amount:N2}").Bold().FontSize(12).FontColor("#14532D");
                    });

                    col.Item().PaddingTop(24);

                    // Settlement & Status Badge
                    if (receipt.PaymentStatus == PaymentStatus.Paid)
                    {
                        col.Item().Border(1.5f).BorderColor("#1F8A55").Background("#eafbf0").Padding(16).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("STATUS: PAID").FontSize(14).Bold().FontColor("#14532D");
                                if (payment != null)
                                {
                                    c.Item().Text($"Payment Method: {payment.PaymentMethod}").FontSize(10);
                                    if (!string.IsNullOrWhiteSpace(payment.TransactionRef))
                                        c.Item().Text($"Transaction Ref: {payment.TransactionRef}").FontSize(10);
                                    c.Item().Text($"Settled At: {payment.PaidAt:dd MMM yyyy, hh:mm tt}").FontSize(10);
                                }
                            });

                            row.ConstantItem(100).AlignMiddle().AlignCenter()
                                .Border(2).BorderColor("#1F8A55").Padding(6)
                                .Text("PAID").FontSize(16).Bold().FontColor("#1F8A55");
                        });
                    }
                    else
                    {
                        col.Item().Border(1.5f).BorderColor("#d97706").Background("#fffbeb").Padding(16).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("STATUS: PAYMENT PENDING").FontSize(14).Bold().FontColor("#b45309");
                                c.Item().Text("Please settle this invoice at the front desk using Cash, Card, or Online transfer.").FontSize(10);
                            });

                            row.ConstantItem(120).AlignMiddle().AlignCenter()
                                .Border(2).BorderColor("#d97706").Padding(6)
                                .Text("PENDING").FontSize(14).Bold().FontColor("#d97706");
                        });
                    }
                });

                // Footer
                page.Footer().BorderTop(1).BorderColor("#e5e5e0").PaddingTop(8).Row(r =>
                {
                    r.RelativeItem().Text("Thank you for choosing DentaCore Dental Clinic.").FontSize(8).FontColor("#888888");
                    r.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}
