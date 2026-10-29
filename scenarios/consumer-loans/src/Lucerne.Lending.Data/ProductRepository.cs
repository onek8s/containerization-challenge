using System.Security.Cryptography;
using System.Text;
using Lucerne.Lending.Domain;

namespace Lucerne.Lending.Data;

public sealed class ProductRepository
{
    private readonly Db _db;

    public ProductRepository(Db db) => _db = db;

    public async Task<IReadOnlyList<LoanProduct>> ListAsync(bool activeOnly = true, CancellationToken ct = default) =>
        (await _db.QueryAsync<ProductRow>(activeOnly ? "SELECT * FROM lending.Product WHERE Active = 1 ORDER BY Code" : "SELECT * FROM lending.Product ORDER BY Code",
            ct: ct)).Select(r => r.ToProduct()).ToList();

    public async Task<LoanProduct?> GetAsync(string code, CancellationToken ct = default) =>
        (await _db.FirstOrDefaultAsync<ProductRow>("SELECT * FROM lending.Product WHERE Code = @code", new { code }, ct: ct))?.ToProduct();

    public Task SaveAsync(LoanProduct p)
    {
        var args = new
        {
            p.Code, p.Name, p.CoreProductCode, p.MinAmount, p.MaxAmount, p.MinTermMonths, p.MaxTermMonths, ReferenceRate = (int)p.ReferenceRate,
            p.BaseMarginPercent, p.ArrangementFee, p.MonthlyFee, p.RequiresCollateral, p.AllowsCoApplicant,
            Purposes = string.Join(',', p.Purposes), p.Active
        };
        return _db.ExecuteAsync(@"UPDATE lending.Product SET Name = @Name, CoreProductCode = @CoreProductCode, MinAmount = @MinAmount, MaxAmount = @MaxAmount,
                MinTermMonths = @MinTermMonths, MaxTermMonths = @MaxTermMonths, ReferenceRate = @ReferenceRate, BaseMarginPercent = @BaseMarginPercent,
                ArrangementFee = @ArrangementFee, MonthlyFee = @MonthlyFee, RequiresCollateral = @RequiresCollateral, AllowsCoApplicant = @AllowsCoApplicant,
                Purposes = @Purposes, Active = @Active WHERE Code = @Code", args)
            .ContinueWith(t => t.Result == 1 ? Task.CompletedTask : _db.ExecuteAsync(@"INSERT INTO lending.Product(Code, Name, CoreProductCode, MinAmount,
                MaxAmount, MinTermMonths, MaxTermMonths, ReferenceRate, BaseMarginPercent, ArrangementFee, MonthlyFee, RequiresCollateral,
                AllowsCoApplicant, Purposes, Active)
                VALUES (@Code, @Name, @CoreProductCode, @MinAmount, @MaxAmount, @MinTermMonths, @MaxTermMonths, @ReferenceRate, @BaseMarginPercent,
                @ArrangementFee, @MonthlyFee, @RequiresCollateral, @AllowsCoApplicant, @Purposes, @Active)", args)).Unwrap();
    }
}

public sealed class ApiClientRepository
{
    private readonly Db _db;

    public ApiClientRepository(Db db) => _db = db;

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public Task<ApiClientRow?> FindAsync(string key) =>
        _db.FirstOrDefaultAsync<ApiClientRow>("SELECT * FROM lending.ApiClient WHERE KeyHash = @hash AND Active = 1", new { hash = Hash(key) });

    public async Task SetAsync(string name, string key, string scopes)
    {
        var rows = await _db.ExecuteAsync("UPDATE lending.ApiClient SET KeyHash = @hash, Scopes = @scopes, Active = 1 WHERE Name = @name",
            new { name, hash = Hash(key), scopes });
        if (rows == 0)
            await _db.ExecuteAsync("INSERT INTO lending.ApiClient(Name, KeyHash, Scopes, Active) VALUES (@name, @hash, @scopes, 1)",
                new { name, hash = Hash(key), scopes });
    }
}
