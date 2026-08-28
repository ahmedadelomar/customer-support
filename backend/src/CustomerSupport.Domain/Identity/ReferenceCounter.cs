using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Identity;

/// <summary>
/// Fallback counter for database providers that have no sequences (SQLite in development).
/// </summary>
/// <remarks>
/// SQL Server uses real sequences created in the initial migration — they are cheaper and do not
/// take a row lock. This table exists only so the same code path works on a provider without them;
/// <c>ReferenceNumberGenerator</c> picks the right strategy from the active provider.
/// </remarks>
public class ReferenceCounter : BaseEntity
{
    /// <summary>Counter name, matching the SQL Server sequence name (e.g. <c>TicketNumbers</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Last value handed out. The next caller receives <c>Value + 1</c>.</summary>
    public long Value { get; set; }
}
