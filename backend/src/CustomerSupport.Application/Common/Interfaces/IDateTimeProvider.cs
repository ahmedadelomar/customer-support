namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>
/// Abstracts the clock. SLA maths is the core of this product, so tests must be able to
/// control time rather than sleep.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }
}
