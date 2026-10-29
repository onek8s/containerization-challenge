using Microsoft.EntityFrameworkCore;

namespace Humongous.Quoting.Data;

/// <summary>Quote and policy number sequences held in the NumberSequences table.</summary>
public sealed class NumberGenerator(QuotingDbContext db)
{
    public const string QuoteSequence = "Quote";
    public const string PolicySequence = "Policy";

    public async Task<long> NextAsync(string name, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var current = await db.NumberSequences.AsNoTracking()
                .Where(s => s.Name == name).Select(s => (long?)s.NextValue).FirstOrDefaultAsync(ct);
            if (current is null)
            {
                // Plain INSERT so that other pending changes in the context are not saved here.
                try
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO NumberSequences (Name, NextValue) VALUES ({name}, {2L})", ct);
                    return 1;
                }
                catch (System.Data.Common.DbException) when (attempt < 5)
                {
                    continue; // created by another process meanwhile
                }
            }

            // Compare-and-swap so two API instances or the policy service never hand out the same number.
            var updated = await db.NumberSequences
                .Where(s => s.Name == name && s.NextValue == current)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextValue, current.Value + 1), ct);
            if (updated == 1)
                return current.Value;
            if (attempt > 20)
                throw new InvalidOperationException($"Could not allocate a number from sequence '{name}'.");
        }
    }
}
