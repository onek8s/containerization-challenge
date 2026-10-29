using Microsoft.EntityFrameworkCore;
using PartsCatalog.Data;
using PartsCatalog.Models;

namespace PartsCatalog.Services;

public class PartService
{
    private readonly CatalogContext _db;

    public PartService(CatalogContext db)
    {
        _db = db;
    }

    public async Task<List<Part>> SearchAsync(string? term, string? category, bool reorderOnly)
    {
        IQueryable<Part> query = _db.Parts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            query = query.Where(p => p.PartNumber.Contains(t) || p.Description.Contains(t));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        if (reorderOnly)
        {
            query = query.Where(p => p.QuantityOnHand <= p.ReorderLevel);
        }

        return await query.OrderBy(p => p.PartNumber).ToListAsync();
    }

    public Task<List<string>> GetCategoriesAsync() =>
        _db.Parts.Where(p => p.Category != null)
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

    public Task<Part?> FindAsync(int id) => _db.Parts.FindAsync(id).AsTask();

    public Task<Part?> FindByNumberAsync(string partNumber) =>
        _db.Parts.AsNoTracking().FirstOrDefaultAsync(p => p.PartNumber == partNumber);

    public Task<bool> PartNumberExistsAsync(string partNumber, int exceptId = 0) =>
        _db.Parts.AnyAsync(p => p.PartNumber == partNumber && p.Id != exceptId);

    public async Task AddAsync(Part part)
    {
        part.PartNumber = part.PartNumber.Trim().ToUpperInvariant();
        part.LastUpdated = DateTime.UtcNow;
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Part part)
    {
        part.PartNumber = part.PartNumber.Trim().ToUpperInvariant();
        part.LastUpdated = DateTime.UtcNow;
        _db.Parts.Update(part);
        await _db.SaveChangesAsync();
    }

    public async Task<Part?> AdjustStockAsync(string partNumber, int delta)
    {
        var part = await _db.Parts.FirstOrDefaultAsync(p => p.PartNumber == partNumber);
        if (part == null)
        {
            return null;
        }

        if (part.QuantityOnHand + delta < 0)
        {
            throw new InvalidOperationException($"Insufficient stock for {partNumber}: on hand {part.QuantityOnHand}, requested {-delta}.");
        }

        part.QuantityOnHand += delta;
        part.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return part;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var part = await _db.Parts.FindAsync(id);
        if (part == null)
        {
            return false;
        }

        _db.Parts.Remove(part);
        await _db.SaveChangesAsync();
        return true;
    }
}
