using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProductReturns.Core;

namespace ProductReturns.Data
{
    public class ReturnSearch
    {
        public ReturnStatus? Status { get; set; }
        public string Text { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class PagedResult<T>
    {
        public IList<T> Items { get; set; } = new List<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class ReturnsRepository
    {
        private static readonly ReturnStatus[] InactiveStatuses = { ReturnStatus.Rejected, ReturnStatus.Cancelled };

        private readonly ReturnsDbContext _db;

        public ReturnsRepository(ReturnsDbContext db)
        {
            _db = db;
        }

        public ReturnsDbContext Db => _db;

        public async Task<SalesOrder> FindOrderAsync(string orderNumber, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
            {
                return null;
            }

            var number = orderNumber.Trim().ToUpperInvariant();
            return await _db.Orders.AsNoTracking()
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.OrderNumber == number, cancellationToken);
        }

        public async Task<IDictionary<int, int>> ReturnedQuantitiesAsync(string orderNumber, CancellationToken cancellationToken = default)
        {
            var lines = await _db.ReturnLines.AsNoTracking()
                .Join(_db.Returns, l => l.ReturnRequestId, r => r.Id, (l, r) => new { l.OrderLineNumber, l.Quantity, l.ReceivedQuantity, r.OrderNumber, r.Status })
                .Where(x => x.OrderNumber == orderNumber)
                .ToListAsync(cancellationToken);

            return lines
                .Where(x => !InactiveStatuses.Contains(x.Status))
                .GroupBy(x => x.OrderLineNumber)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Status == ReturnStatus.Closed ? x.ReceivedQuantity : x.Quantity));
        }

        public Task<List<CatalogItem>> LoadCatalogAsync(CancellationToken cancellationToken = default)
        {
            return _db.Catalog.AsNoTracking().ToListAsync(cancellationToken);
        }

        public async Task<ReturnRequest> CreateAsync(ReturnRequest request, CancellationToken cancellationToken = default)
        {
            // The SQL Server provider retries on transient errors, so the transaction runs inside its execution strategy.
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken))
                {
                    request.RmaNumber = null;
                    _db.Returns.Add(request);
                    await _db.SaveChangesAsync(cancellationToken);
                    request.RmaNumber = RmaNumber.Create(request.Id, request.CreatedUtc);
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }

                return request;
            });
        }

        public Task<ReturnRequest> GetAsync(string rmaNumber, CancellationToken cancellationToken = default)
        {
            if (!RmaNumber.TryNormalize(rmaNumber, out var rma))
            {
                return Task.FromResult<ReturnRequest>(null);
            }

            return _db.Returns
                .Include(r => r.Lines)
                .Include(r => r.Notes)
                .FirstOrDefaultAsync(r => r.RmaNumber == rma, cancellationToken);
        }

        public Task<ReturnRequest> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return _db.Returns
                .Include(r => r.Lines)
                .Include(r => r.Notes)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<PagedResult<ReturnRequest>> SearchAsync(ReturnSearch search, CancellationToken cancellationToken = default)
        {
            var query = _db.Returns.AsNoTracking().AsQueryable();
            if (search.Status.HasValue)
            {
                query = query.Where(r => r.Status == search.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search.Text))
            {
                var text = search.Text.Trim();
                query = query.Where(r => r.RmaNumber.Contains(text) || r.OrderNumber.Contains(text)
                    || r.CustomerEmail.Contains(text) || r.CustomerName.Contains(text) || r.TrackingNumber == text);
            }

            var page = Math.Max(1, search.Page);
            var size = Math.Min(Math.Max(5, search.PageSize), 200);
            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderByDescending(r => r.CreatedUtc)
                .Skip((page - 1) * size).Take(size)
                .Include(r => r.Lines)
                .ToListAsync(cancellationToken);

            return new PagedResult<ReturnRequest> { Items = items, Total = total, Page = page, PageSize = size };
        }

        public async Task<IDictionary<ReturnStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default)
        {
            var counts = await _db.Returns.AsNoTracking()
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
            return counts.ToDictionary(c => c.Status, c => c.Count);
        }

        public Task<List<ReturnRequest>> PendingLabelsAsync(int maxAttempts, int take, CancellationToken cancellationToken = default)
        {
            return _db.Returns
                .Include(r => r.Lines)
                .Where(r => r.Status == ReturnStatus.Approved && r.TrackingNumber == null && r.LabelAttempts < maxAttempts)
                .OrderBy(r => r.DecidedUtc)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<List<ReturnRequest>> RefundsDueAsync(DateTime inspectedBeforeUtc, CancellationToken cancellationToken = default)
        {
            return _db.Returns
                .Include(r => r.Lines)
                .Where(r => r.Status == ReturnStatus.Inspected && r.RefundExportedUtc == null && r.InspectedUtc < inspectedBeforeUtc)
                .OrderBy(r => r.InspectedUtc)
                .ToListAsync(cancellationToken);
        }

        public Task<List<ReturnRequest>> AwaitingReceiptAsync(DateTime labelBeforeUtc, CancellationToken cancellationToken = default)
        {
            return _db.Returns
                .Where(r => r.Status == ReturnStatus.LabelIssued && r.LabelCreatedUtc < labelBeforeUtc)
                .OrderBy(r => r.LabelCreatedUtc)
                .ToListAsync(cancellationToken);
        }

        public Task<List<ReturnRequest>> ClosedBeforeAsync(DateTime createdBeforeUtc, CancellationToken cancellationToken = default)
        {
            var closed = new[] { ReturnStatus.Closed, ReturnStatus.RefundExported, ReturnStatus.Rejected, ReturnStatus.Cancelled };
            return _db.Returns
                .Where(r => closed.Contains(r.Status) && r.AnonymizedUtc == null && r.CreatedUtc < createdBeforeUtc)
                .ToListAsync(cancellationToken);
        }

        public Task<ScannerDevice> FindScannerAsync(string apiKeyHash, CancellationToken cancellationToken = default)
        {
            return _db.Scanners.FirstOrDefaultAsync(s => s.ApiKeyHash == apiKeyHash && s.Enabled, cancellationToken);
        }

        public Task<int> SaveAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
    }
}
