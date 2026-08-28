using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Allocates gap-free reference numbers from a database sequence rather than counting rows.
/// Counting rows would race under concurrent inserts and would reuse numbers after deletes.
/// </summary>
/// <remarks>
/// Three strategies, chosen from the active provider:
/// <list type="bullet">
///   <item>SQL Server — a real sequence (<c>NEXT VALUE FOR</c>), created in the initial migration.</item>
///   <item>SQLite (development) — a <see cref="ReferenceCounter"/> row incremented inside a transaction,
///         because SQLite has no sequences.</item>
///   <item>In-memory (tests) — a time-based value; tests need uniqueness, not density.</item>
/// </list>
/// </remarks>
public class ReferenceNumberGenerator(AppDbContext db, IDateTimeProvider clock) : IReferenceNumberGenerator
{
    public async Task<string> NextTicketNumberAsync(CancellationToken ct = default)
    {
        var next = await NextValueAsync("TicketNumbers", ct);
        return $"TCK-{clock.UtcNow.Year}-{next:D6}";
    }

    public async Task<string> NextCustomerCodeAsync(CancellationToken ct = default)
    {
        var next = await NextValueAsync("CustomerCodes", ct);
        return $"CUS-{next:D6}";
    }

    private async Task<long> NextValueAsync(string name, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            // The in-memory provider has neither sequences nor real transactions.
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000;
        }

        return db.Database.IsSqlServer()
            ? await NextFromSequenceAsync(name, ct)
            : await NextFromCounterTableAsync(name, ct);
    }

    /// <summary>SQL Server: read the next value from the sequence created in the initial migration.</summary>
    private async Task<long> NextFromSequenceAsync(string sequenceName, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT NEXT VALUE FOR [dbo].[{sequenceName}]";

        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(ct);
        }

        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Providers without sequences: increment a counter row inside a transaction so two concurrent
    /// callers cannot receive the same number.
    /// </summary>
    private async Task<long> NextFromCounterTableAsync(string name, CancellationToken ct)
    {
        // Reuse an ambient transaction when the caller already opened one.
        IDbContextTransaction? owned = null;
        if (db.Database.CurrentTransaction is null)
        {
            owned = await db.Database.BeginTransactionAsync(ct);
        }

        try
        {
            var counter = await db.Set<ReferenceCounter>()
                .FirstOrDefaultAsync(c => c.Name == name, ct);

            if (counter is null)
            {
                counter = new ReferenceCounter { Name = name, Value = 0 };
                db.Set<ReferenceCounter>().Add(counter);
            }

            counter.Value += 1;
            await db.SaveChangesAsync(ct);

            if (owned is not null)
            {
                await owned.CommitAsync(ct);
            }

            return counter.Value;
        }
        catch
        {
            if (owned is not null)
            {
                await owned.RollbackAsync(ct);
            }

            throw;
        }
        finally
        {
            if (owned is not null)
            {
                await owned.DisposeAsync();
            }
        }
    }
}
