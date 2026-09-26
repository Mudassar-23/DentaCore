namespace DentaCore.Application.Interfaces;

public interface IPdfReceiptService
{
    Task<byte[]> GenerateReceiptPdfBytesAsync(Guid receiptId, CancellationToken cancellationToken = default);
}
