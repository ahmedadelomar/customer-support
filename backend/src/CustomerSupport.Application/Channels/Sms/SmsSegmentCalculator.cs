namespace CustomerSupport.Application.Channels.Sms;

/// <summary>
/// One text's segment cost. <see cref="Length"/> counts GSM extended characters as two septets each
/// (or is the raw character count under UCS-2); <see cref="SegmentCount"/> is how many SMS parts the
/// provider will actually bill for.
/// </summary>
public record SmsSegments(int Length, int SegmentCount, string Encoding, int MaxLength);

/// <summary>
/// GSM-7 vs UCS-2 segment math (Communication Channels / SMS channel, CS-304). Mirrored exactly in
/// TypeScript (`sms-segment-calculator.ts`) against the same shared test-vector fixture — the server
/// is authoritative for what actually gets billed, but the two must never disagree, or an agent sees
/// one number while composing and is charged for another.
/// </summary>
public static class SmsSegmentCalculator
{
    /// <summary>The GSM 03.38 default alphabet's basic character set — one septet each.</summary>
    private const string GsmBasic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

    /// <summary>Extension table characters — each costs two septets (an escape sequence plus the character).</summary>
    private const string GsmExtended = "^{}\\[~]|€";

    private static readonly HashSet<char> GsmCharset = new(GsmBasic + GsmExtended);
    private static readonly HashSet<char> GsmExtendedCharset = new(GsmExtended);

    public static SmsSegments Calculate(string text)
    {
        text ??= string.Empty;

        // A single character outside the GSM-7 alphabet forces the whole message to UCS-2 — there
        // is no mixed encoding on the wire.
        var isGsm = text.All(GsmCharset.Contains);

        var perSegment = isGsm ? 160 : 70;
        var perConcatenated = isGsm ? 153 : 67;
        var length = isGsm ? text.Sum(c => GsmExtendedCharset.Contains(c) ? 2 : 1) : text.Length;

        var segments = length <= perSegment ? 1 : (int)Math.Ceiling(length / (double)perConcatenated);

        return new SmsSegments(length, segments, isGsm ? "GSM-7" : "UCS-2", perSegment);
    }
}
