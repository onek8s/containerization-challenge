using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Catalog.Api.Data;
using VanArsdel.Catalog.Api.Domain;

namespace VanArsdel.Catalog.Api.Features.Products
{
    public class GetProducts : IRequest<PagedResult<ProductSummaryDto>>
    {
        public string Search { get; set; }
        public string Category { get; set; }
        public string Brand { get; set; }
        public string Season { get; set; }
        public string Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class GetProduct : IRequest<ProductDto>
    {
        public string StyleCodeOrId { get; set; }
    }

    public class LookupSkus : IRequest<List<SkuLookupDto>>
    {
        public List<string> Codes { get; set; } = new List<string>();
    }

    public class ProductQueryHandlers :
        IRequestHandler<GetProducts, PagedResult<ProductSummaryDto>>,
        IRequestHandler<GetProduct, ProductDto>,
        IRequestHandler<LookupSkus, List<SkuLookupDto>>
    {
        private readonly CatalogDbContext _db;
        private readonly IMapper _mapper;
        private readonly IDistributedCache _cache;

        public ProductQueryHandlers(CatalogDbContext db, IMapper mapper, IDistributedCache cache)
        {
            _db = db;
            _mapper = mapper;
            _cache = cache;
        }

        public async Task<PagedResult<ProductSummaryDto>> Handle(GetProducts request, CancellationToken cancellationToken)
        {
            var query = _db.Products.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim();
                query = query.Where(p => p.StyleCode.StartsWith(s) || p.Name.Contains(s) || p.Skus.Any(k => k.Ean == s || k.SkuCode == s));
            }
            if (!string.IsNullOrEmpty(request.Category)) query = query.Where(p => p.Category.Code == request.Category || p.Category.Parent.Code == request.Category);
            if (!string.IsNullOrEmpty(request.Brand)) query = query.Where(p => p.Brand.Code == request.Brand);
            if (!string.IsNullOrEmpty(request.Season)) query = query.Where(p => p.Season == request.Season);
            if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<ProductStatus>(request.Status, true, out var status))
            {
                query = query.Where(p => p.Status == status);
            }

            var pageSize = Math.Clamp(request.PageSize, 1, 200);
            var page = Math.Max(request.Page, 1);
            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(p => p.StyleCode)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ProjectTo<ProductSummaryDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductSummaryDto> { Page = page, PageSize = pageSize, Total = total, Items = items };
        }

        public async Task<ProductDto> Handle(GetProduct request, CancellationToken cancellationToken)
        {
            var key = "product:" + request.StyleCodeOrId.ToUpperInvariant();
            var dto = await _cache.GetOrSetJsonAsync(key, TimeSpan.FromMinutes(10), async () =>
            {
                var query = _db.Products.AsNoTracking()
                    .Include(p => p.Brand).Include(p => p.Category).Include(p => p.Skus).Include(p => p.Images);
                var product = Guid.TryParse(request.StyleCodeOrId, out var id)
                    ? await query.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
                    : await query.SingleOrDefaultAsync(p => p.StyleCode == request.StyleCodeOrId, cancellationToken);
                return product == null ? null : _mapper.Map<ProductDto>(product);
            }, cancellationToken);

            return dto ?? throw new NotFoundException("Product", request.StyleCodeOrId);
        }

        public async Task<List<SkuLookupDto>> Handle(LookupSkus request, CancellationToken cancellationToken)
        {
            var codes = request.Codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim().ToUpperInvariant()).Distinct().Take(500).ToList();
            var rows = await (from s in _db.Skus.AsNoTracking()
                              join p in _db.Products.AsNoTracking() on s.ProductId equals p.Id
                              where codes.Contains(s.SkuCode) || codes.Contains(s.Ean)
                              select new SkuLookupDto
                              {
                                  SkuCode = s.SkuCode,
                                  Ean = s.Ean,
                                  StyleCode = p.StyleCode,
                                  Name = p.Name,
                                  ColourName = s.ColourName,
                                  Size = s.Size,
                                  WeightKg = s.WeightKg,
                                  RecommendedPriceEur = p.RecommendedPriceEur,
                              }).ToListAsync(cancellationToken);
            return rows;
        }
    }
}
