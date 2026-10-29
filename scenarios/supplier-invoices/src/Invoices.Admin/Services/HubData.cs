using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Wingtip.Invoices.Admin.Services;

public class InvoiceSearch
{
    public byte? Status { get; set; }
    public int? SupplierId { get; set; }
    public string Text { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class HubData
{
    private readonly string connectionString;

    public HubData(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("Invoices");
    }

    private SqlConnection Open() => new SqlConnection(connectionString);

    public async Task<IList<StatusCount>> StatusCountsAsync()
    {
        using var cn = Open();
        return (await cn.QueryAsync<StatusCount>(
            "SELECT Status, COUNT(*) AS Count, SUM(GrossAmount) AS Gross FROM inv.Invoice WHERE ReceivedAt >= DATEADD(day, -90, SYSDATETIME()) OR Status IN (1, 2, 3, 4, 6) GROUP BY Status")).ToList();
    }

    public async Task<IList<IssueCount>> OpenIssueCountsAsync()
    {
        using var cn = Open();
        return (await cn.QueryAsync<IssueCount>(
            @"SELECT x.Code, COUNT(DISTINCT x.InvoiceId) AS Count, MIN(i.ReceivedAt) AS Oldest
              FROM inv.InvoiceIssue x JOIN inv.Invoice i ON i.InvoiceId = x.InvoiceId
              WHERE x.Resolved = 0 AND i.Status = 2
              GROUP BY x.Code ORDER BY Count DESC")).ToList();
    }

    public async Task<(IList<InvoiceRow> Rows, int Total)> SearchAsync(InvoiceSearch search, string issueCode = null)
    {
        var where = new List<string>();
        var p = new DynamicParameters();
        if (search.Status.HasValue) { where.Add("Status = @Status"); p.Add("Status", search.Status); }
        if (search.SupplierId.HasValue) { where.Add("SupplierId = @SupplierId"); p.Add("SupplierId", search.SupplierId); }
        if (search.From.HasValue) { where.Add("InvoiceDate >= @From"); p.Add("From", search.From, DbType.Date); }
        if (search.To.HasValue) { where.Add("InvoiceDate <= @To"); p.Add("To", search.To, DbType.Date); }
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            where.Add("(InvoiceNumber LIKE @Text OR PurchaseOrderNumber LIKE @Text OR SupplierName LIKE @Text OR VoucherNumber LIKE @Text OR PaymentReference LIKE @Text)");
            p.Add("Text", "%" + search.Text.Trim() + "%", DbType.String, size: 102);
        }
        if (issueCode != null)
        {
            where.Add("InvoiceId IN (SELECT InvoiceId FROM inv.InvoiceIssue WHERE Code = @Code AND Resolved = 0)");
            p.Add("Code", issueCode, DbType.AnsiString, size: 30);
        }

        var filter = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        p.Add("Skip", (Math.Max(search.Page, 1) - 1) * search.PageSize);
        p.Add("Take", search.PageSize);

        using var cn = Open();
        using var multi = await cn.QueryMultipleAsync(
            $@"SELECT COUNT(*) FROM inv.InvoiceOverview {filter};
               SELECT * FROM inv.InvoiceOverview {filter} ORDER BY ReceivedAt DESC, InvoiceId DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;", p);
        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<InvoiceRow>()).ToList();
        return (rows, total);
    }

    public async Task<InvoiceRow> InvoiceAsync(long invoiceId)
    {
        using var cn = Open();
        return await cn.QuerySingleOrDefaultAsync<InvoiceRow>("SELECT * FROM inv.InvoiceOverview WHERE InvoiceId = @invoiceId", new { invoiceId });
    }

    public async Task<(IList<LineRow> Lines, IList<IssueRow> Issues, IList<HistoryRow> History)> InvoiceDetailsAsync(long invoiceId)
    {
        using var cn = Open();
        using var multi = await cn.QueryMultipleAsync(
            @"SELECT * FROM inv.InvoiceLine WHERE InvoiceId = @invoiceId ORDER BY LineNumber;
              SELECT Code, Message, Resolved FROM inv.InvoiceIssue WHERE InvoiceId = @invoiceId ORDER BY Resolved, IssueId;
              SELECT At, UserName, Action, Comment FROM inv.InvoiceHistory WHERE InvoiceId = @invoiceId ORDER BY At, HistoryId;",
            new { invoiceId });
        return ((await multi.ReadAsync<LineRow>()).ToList(), (await multi.ReadAsync<IssueRow>()).ToList(), (await multi.ReadAsync<HistoryRow>()).ToList());
    }

    public async Task RejectAsync(long invoiceId, string user, string reason)
    {
        using var cn = Open();
        await cn.ExecuteAsync("inv.RejectInvoice", new { InvoiceId = invoiceId, User = user, Reason = reason }, commandType: CommandType.StoredProcedure);
    }

    public async Task AddCommentAsync(long invoiceId, string user, string comment)
    {
        using var cn = Open();
        await cn.ExecuteAsync("inv.AddInvoiceHistory", new { InvoiceId = invoiceId, User = user, Action = "Comment", Comment = comment }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IList<FileRow>> FilesAsync(string status, int? supplierId, int take = 200)
    {
        using var cn = Open();
        return (await cn.QueryAsync<FileRow>(
            @"SELECT TOP (@take) f.FileId, s.Code AS SupplierCode, c.ChannelType, f.FileName, f.SizeBytes, f.ArchivePath, f.ReceivedAt, f.Status, f.Attempts, f.LastError,
                     (SELECT COUNT(*) FROM inv.Invoice i WHERE i.FileId = f.FileId) AS Invoices
              FROM inv.InboundFile f
              JOIN inv.Supplier s ON s.SupplierId = f.SupplierId
              JOIN inv.Channel c ON c.ChannelId = f.ChannelId
              WHERE (@status IS NULL OR f.Status = @status) AND (@supplierId IS NULL OR f.SupplierId = @supplierId)
              ORDER BY f.FileId DESC",
            new { take, status, supplierId })).ToList();
    }

    public async Task<FileRow> FileAsync(long fileId)
    {
        using var cn = Open();
        return await cn.QuerySingleOrDefaultAsync<FileRow>(
            @"SELECT f.FileId, s.Code AS SupplierCode, f.FileName, f.SizeBytes, f.ArchivePath, f.ReceivedAt, f.Status, f.Attempts, f.LastError
              FROM inv.InboundFile f JOIN inv.Supplier s ON s.SupplierId = f.SupplierId WHERE f.FileId = @fileId", new { fileId });
    }

    public async Task<IList<SupplierRow>> SuppliersAsync()
    {
        using var cn = Open();
        return (await cn.QueryAsync<SupplierRow>("SELECT * FROM inv.Supplier ORDER BY Code")).ToList();
    }

    public async Task<SupplierRow> SupplierAsync(int supplierId)
    {
        using var cn = Open();
        return await cn.QuerySingleOrDefaultAsync<SupplierRow>("SELECT * FROM inv.Supplier WHERE SupplierId = @supplierId", new { supplierId });
    }

    public async Task<int> SaveSupplierAsync(SupplierRow s, string user)
    {
        using var cn = Open();
        if (s.SupplierId == 0)
        {
            return await cn.ExecuteScalarAsync<int>(
                @"INSERT inv.Supplier (Code, Name, VatNumber, VendorNumber, CodePage, PriceTolerancePercent, TwoWayMatch, CostCenter, ContactEmail, Active, ModifiedBy)
                  VALUES (@Code, @Name, @VatNumber, @VendorNumber, @CodePage, @PriceTolerancePercent, @TwoWayMatch, @CostCenter, @ContactEmail, @Active, @user);
                  SELECT CAST(SCOPE_IDENTITY() AS int);",
                new { s.Code, s.Name, s.VatNumber, s.VendorNumber, s.CodePage, s.PriceTolerancePercent, s.TwoWayMatch, s.CostCenter, s.ContactEmail, s.Active, user });
        }

        await cn.ExecuteAsync(
            @"UPDATE inv.Supplier SET Name = @Name, VatNumber = @VatNumber, VendorNumber = @VendorNumber, CodePage = @CodePage,
                     PriceTolerancePercent = @PriceTolerancePercent, TwoWayMatch = @TwoWayMatch, CostCenter = @CostCenter,
                     ContactEmail = @ContactEmail, Active = @Active, ModifiedBy = @user, ModifiedAt = SYSDATETIME()
              WHERE SupplierId = @SupplierId",
            new { s.SupplierId, s.Name, s.VatNumber, s.VendorNumber, s.CodePage, s.PriceTolerancePercent, s.TwoWayMatch, s.CostCenter, s.ContactEmail, s.Active, user });
        return s.SupplierId;
    }

    public async Task<IList<ChannelRow>> ChannelsAsync(int? supplierId = null)
    {
        using var cn = Open();
        return (await cn.QueryAsync<ChannelRow>(
            @"SELECT c.*, s.Code AS SupplierCode FROM inv.Channel c JOIN inv.Supplier s ON s.SupplierId = c.SupplierId
              WHERE @supplierId IS NULL OR c.SupplierId = @supplierId ORDER BY s.Code, c.ChannelId", new { supplierId })).ToList();
    }

    public async Task SaveChannelAsync(ChannelRow c)
    {
        using var cn = Open();
        if (c.ChannelId == 0)
        {
            await cn.ExecuteAsync(
                "INSERT inv.Channel (SupplierId, ChannelType, Location, FilePattern, Active) VALUES (@SupplierId, @ChannelType, @Location, @FilePattern, @Active)", c);
        }
        else
        {
            await cn.ExecuteAsync(
                "UPDATE inv.Channel SET ChannelType = @ChannelType, Location = @Location, FilePattern = @FilePattern, Active = @Active WHERE ChannelId = @ChannelId AND SupplierId = @SupplierId", c);
        }
    }

    public async Task<IList<BatchRow>> BatchesAsync(int take = 100)
    {
        using var cn = Open();
        return (await cn.QueryAsync<BatchRow>(
            @"SELECT TOP (@take) b.*,
                     (SELECT COUNT(*) FROM inv.Invoice i WHERE i.BatchId = b.BatchId AND i.Status = 5) AS Posted,
                     (SELECT COUNT(*) FROM inv.Invoice i WHERE i.BatchId = b.BatchId AND i.Status = 6) AS Rejected
              FROM inv.PostingBatch b ORDER BY b.BatchId DESC", new { take })).ToList();
    }
}
