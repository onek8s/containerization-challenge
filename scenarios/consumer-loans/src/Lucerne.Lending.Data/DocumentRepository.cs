using System.Data;
using System.Security.Cryptography;

namespace Lucerne.Lending.Data;

public sealed class DocumentRepository
{
    private readonly Db _db;

    public DocumentRepository(Db db) => _db = db;

    public Task<long> AddAsync(DocumentRow d, IDbTransaction? tx = null)
    {
        d.CreatedUtc = DateTime.UtcNow;
        d.Sha256 = d.Content == null ? null : Convert.ToHexString(SHA256.HashData(d.Content)).ToLowerInvariant();
        return _db.ScalarAsync<long>(@"INSERT INTO lending.Document(ApplicationId, CollectionCaseId, Kind, FileName, ContentType, Content, Sha256, Status,
                SigningOrderId, SigningUrl, CreatedUtc)
            VALUES (@ApplicationId, @CollectionCaseId, @Kind, @FileName, @ContentType, @Content, @Sha256, @Status, @SigningOrderId, @SigningUrl, @CreatedUtc);
            SELECT CAST(SCOPE_IDENTITY() AS bigint);", d, tx);
    }

    public Task<DocumentRow?> GetAsync(long id, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<DocumentRow>("SELECT * FROM lending.Document WHERE Id = @id", new { id }, ct: ct);

    public Task<IReadOnlyList<DocumentRow>> ListForApplicationAsync(long applicationId, CancellationToken ct = default) =>
        _db.QueryAsync<DocumentRow>(@"SELECT Id, ApplicationId, CollectionCaseId, Kind, FileName, ContentType, Sha256, Status, SigningOrderId,
                SigningUrl, CreatedUtc, SignedUtc FROM lending.Document WHERE ApplicationId = @applicationId ORDER BY Id", new { applicationId }, ct: ct);

    public Task<IReadOnlyList<DocumentRow>> ListForCaseAsync(long caseId, CancellationToken ct = default) =>
        _db.QueryAsync<DocumentRow>(@"SELECT Id, ApplicationId, CollectionCaseId, Kind, FileName, ContentType, Sha256, Status, SigningOrderId,
                SigningUrl, CreatedUtc, SignedUtc FROM lending.Document WHERE CollectionCaseId = @caseId ORDER BY Id", new { caseId }, ct: ct);

    public Task<DocumentRow?> LatestAsync(long applicationId, string kind, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<DocumentRow>("SELECT TOP (1) * FROM lending.Document WHERE ApplicationId = @applicationId AND Kind = @kind ORDER BY Id DESC",
            new { applicationId, kind }, ct: ct);

    public Task<DocumentRow?> FindBySigningOrderAsync(string orderId, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<DocumentRow>("SELECT TOP (1) * FROM lending.Document WHERE SigningOrderId = @orderId ORDER BY Id DESC", new { orderId }, ct: ct);

    public Task<IReadOnlyList<DocumentRow>> AwaitingSignatureAsync(CancellationToken ct = default) =>
        _db.QueryAsync<DocumentRow>(@"SELECT Id, ApplicationId, Kind, FileName, Status, SigningOrderId, SigningUrl, CreatedUtc
            FROM lending.Document WHERE Status = 'SentForSigning'", ct: ct);

    public Task SetSigningAsync(long id, string orderId, string url, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Document SET Status = 'SentForSigning', SigningOrderId = @orderId, SigningUrl = @url WHERE Id = @id",
            new { id, orderId, url }, tx);

    public Task SetStatusAsync(long id, string status, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Document SET Status = @status, SignedUtc = CASE WHEN @status = 'Signed' THEN @now ELSE SignedUtc END WHERE Id = @id",
            new { id, status, now = DateTime.UtcNow }, tx);
}
