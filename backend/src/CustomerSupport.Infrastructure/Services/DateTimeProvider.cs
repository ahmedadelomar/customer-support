using CustomerSupport.Application.Common.Interfaces;

namespace CustomerSupport.Infrastructure.Services;

/// <inheritdoc />
public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
