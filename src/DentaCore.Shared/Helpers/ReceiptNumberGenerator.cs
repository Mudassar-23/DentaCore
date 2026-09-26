namespace DentaCore.Shared.Helpers;

/// <summary>
/// Generates sequential, traceable receipt numbers: RCPT-yyyyMMdd-XXXX
/// Thread-safe via Interlocked.
/// </summary>
public static class ReceiptNumberGenerator
{
    private static int _sequence;

    public static string Generate()
    {
        var seq = System.Threading.Interlocked.Increment(ref _sequence);
        return $"RCPT-{DateTime.UtcNow:yyyyMMdd}-{seq:D4}";
    }
}
