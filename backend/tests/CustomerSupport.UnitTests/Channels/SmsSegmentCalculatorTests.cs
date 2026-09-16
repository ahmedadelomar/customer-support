using System.Runtime.CompilerServices;
using System.Text.Json;
using CustomerSupport.Application.Channels.Sms;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.UnitTests.Channels;

public record SmsSegmentVector(
    string Name, string? Text, string? RepeatChar, int? RepeatCharTimes, string? Suffix,
    int Length, int Segments, string Encoding)
{
    public string BuildText() =>
        Text ?? string.Concat(Enumerable.Repeat(RepeatChar!, RepeatCharTimes!.Value)) + (Suffix ?? "");
}

public record SmsSegmentFixture(string Description, List<SmsSegmentVector> Vectors);

/// <summary>
/// GSM-7/UCS-2 segment math (Communication Channels / SMS channel, CS-304), against the same
/// `test-fixtures/sms-segment-vectors.json` the TypeScript mirror's spec reads — the two
/// implementations must never quietly disagree about what an agent will be billed for.
/// </summary>
public class SmsSegmentCalculatorTests
{
    public static IEnumerable<object[]> Vectors() =>
        LoadFixture().Vectors.Select(v => new object[] { v });

    [Theory]
    [MemberData(nameof(Vectors))]
    public void MatchesSharedFixture(SmsSegmentVector vector)
    {
        var result = SmsSegmentCalculator.Calculate(vector.BuildText());

        result.Length.Should().Be(vector.Length, $"vector '{vector.Name}' length");
        result.SegmentCount.Should().Be(vector.Segments, $"vector '{vector.Name}' segments");
        result.Encoding.Should().Be(vector.Encoding, $"vector '{vector.Name}' encoding");
    }

    private static SmsSegmentFixture LoadFixture([CallerFilePath] string here = "")
    {
        // backend/tests/CustomerSupport.UnitTests/Channels/ -> repo root is four levels up.
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", ".."));
        var path = Path.Combine(repoRoot, "test-fixtures", "sms-segment-vectors.json");
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<SmsSegmentFixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })!;
    }
}
