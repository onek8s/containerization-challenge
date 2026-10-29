using ExpenseClaims.Core.Claims;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Api.Services;

public sealed class ClaimNumberAllocator
{
    private readonly ExpenseClaimsDbContext _db;

    public ClaimNumberAllocator(ExpenseClaimsDbContext db) => _db = db;

    /// <summary>Takes the next number of the year. Retries when another request took the same number.</summary>
    public async Task<string> NextAsync(int year, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var sequence = await _db.ClaimSequences.SingleOrDefaultAsync(s => s.Year == year, cancellationToken);
            if (sequence is null)
            {
                sequence = new ClaimSequence { Year = year, LastNumber = 0 };
                _db.ClaimSequences.Add(sequence);
            }
            sequence.LastNumber++;
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return ClaimNumber.Format(year, sequence.LastNumber);
            }
            catch (DbUpdateException) when (attempt < 5)
            {
                _db.Entry(sequence).State = EntityState.Detached;
            }
        }
    }
}
