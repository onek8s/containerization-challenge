using FreightRates.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Data.Services;

public class CustomerService
{
    private readonly FreightRatesContext _db;

    public CustomerService(FreightRatesContext db) => _db = db;

    public Task<Customer?> FindByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var hash = ApiKeys.Hash(apiKey);
        return _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.ApiKeyHash == hash && c.Active, cancellationToken);
    }

    /// <summary>Issues a new API key for the customer and returns it; only the hash is stored.</summary>
    public async Task<string> RotateApiKeyAsync(int customerId, string modifiedBy, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.FindAsync(new object[] { customerId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer {customerId} not found.");
        var key = ApiKeys.Generate();
        customer.ApiKeyHash = ApiKeys.Hash(key);
        customer.ModifiedUtc = DateTime.UtcNow;
        customer.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(cancellationToken);
        return key;
    }
}
