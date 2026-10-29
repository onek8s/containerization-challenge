using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Catalog.Api.Data;
using VanArsdel.Catalog.Api.Domain;
using VanArsdel.Catalog.Api.Search;
using VanArsdel.Contracts.Catalog;

namespace VanArsdel.Catalog.Api.Features.Products
{
    public class SaveProduct : IRequest<ProductDto>
    {
        /// <summary>Empty when creating.</summary>
        public Guid? Id { get; set; }
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string BrandCode { get; set; }
        public string CategoryCode { get; set; }
        public string Season { get; set; }
        public string Gender { get; set; }
        public string Material { get; set; }
        public string CareInstructions { get; set; }
        public string VatClass { get; set; } = "Standard";
        public decimal RecommendedPriceEur { get; set; }
        public decimal RecommendedPriceSek { get; set; }
        public string RowVersion { get; set; }
        public List<SaveSku> Skus { get; set; } = new List<SaveSku>();
        public List<string> Images { get; set; } = new List<string>();
        public bool Publish { get; set; }
    }

    public class SaveSku
    {
        public string ColourCode { get; set; }
        public string ColourName { get; set; }
        public string Size { get; set; }
        public string Ean { get; set; }
        public decimal CostPrice { get; set; }
        public decimal WeightKg { get; set; }
        public bool Blocked { get; set; }
    }

    public class SaveProductValidator : AbstractValidator<SaveProduct>
    {
        private static readonly string[] Seasons = { "SS", "AW", "CORE" };

        public SaveProductValidator()
        {
            RuleFor(x => x.StyleCode).NotEmpty().Matches("^[A-Z0-9]{4,12}$").WithMessage("Style code is 4-12 upper-case letters or digits.");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.BrandCode).NotEmpty();
            RuleFor(x => x.CategoryCode).NotEmpty();
            RuleFor(x => x.Season).NotEmpty().Must(s => s != null && Seasons.Any(p => s.StartsWith(p, StringComparison.Ordinal)))
                .WithMessage("Season is SSyy, AWyy or CORE.");
            RuleFor(x => x.RecommendedPriceEur).GreaterThanOrEqualTo(0);
            RuleFor(x => x.RecommendedPriceSek).GreaterThanOrEqualTo(0);
            RuleFor(x => x.VatClass).Must(v => v == "Standard" || v == "Reduced" || v == "Children")
                .WithMessage("VAT class is Standard, Reduced or Children.");
            RuleForEach(x => x.Skus).ChildRules(s =>
            {
                s.RuleFor(k => k.ColourCode).NotEmpty().MaximumLength(10);
                s.RuleFor(k => k.Size).NotEmpty().MaximumLength(10);
                s.RuleFor(k => k.Ean).Must(e => string.IsNullOrEmpty(e) || Ean13.IsValid(e)).WithMessage("EAN must be a valid EAN-13.");
                s.RuleFor(k => k.CostPrice).GreaterThanOrEqualTo(0);
            });
            RuleFor(x => x.Skus).Must(s => s.Select(k => k.ColourCode + "/" + k.Size).Distinct().Count() == s.Count)
                .WithMessage("Colour and size combinations must be unique.");
        }
    }

    public class PublishProduct : IRequest<ProductDto>
    {
        public string StyleCode { get; set; }
    }

    public class DiscontinueProduct : IRequest<ProductDto>
    {
        public string StyleCode { get; set; }
        public DateTime EffectiveDate { get; set; }
    }

    public class ProductCommandHandlers :
        IRequestHandler<SaveProduct, ProductDto>,
        IRequestHandler<PublishProduct, ProductDto>,
        IRequestHandler<DiscontinueProduct, ProductDto>
    {
        private static readonly string[] SizeOrder = { "XXS", "XS", "S", "M", "L", "XL", "XXL", "3XL", "ONE" };

        private readonly CatalogDbContext _db;
        private readonly IMapper _mapper;
        private readonly IOutbox _outbox;
        private readonly IDistributedCache _cache;
        private readonly IProductSearch _search;

        public ProductCommandHandlers(CatalogDbContext db, IMapper mapper, IOutbox outbox, IDistributedCache cache, IProductSearch search)
        {
            _db = db;
            _mapper = mapper;
            _outbox = outbox;
            _cache = cache;
            _search = search;
        }

        public async Task<ProductDto> Handle(SaveProduct request, CancellationToken cancellationToken)
        {
            var brand = await _db.Brands.SingleOrDefaultAsync(b => b.Code == request.BrandCode, cancellationToken)
                        ?? throw new DomainException($"Unknown brand {request.BrandCode}.");
            var category = await _db.Categories.SingleOrDefaultAsync(c => c.Code == request.CategoryCode, cancellationToken)
                           ?? throw new DomainException($"Unknown category {request.CategoryCode}.");
            var now = DateTime.UtcNow;

            Product product;
            if (request.Id.HasValue)
            {
                product = await Load(p => p.Id == request.Id.Value, cancellationToken) ?? throw new NotFoundException("Product", request.Id);
                if (!string.IsNullOrEmpty(request.RowVersion))
                {
                    _db.Entry(product).Property(p => p.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion);
                }
                if (product.StyleCode != request.StyleCode) throw new DomainException("The style code cannot be changed.");
            }
            else
            {
                if (await _db.Products.AnyAsync(p => p.StyleCode == request.StyleCode, cancellationToken))
                {
                    throw new ConflictException($"Style {request.StyleCode} already exists.");
                }
                product = new Product { Id = Guid.NewGuid(), StyleCode = request.StyleCode, CreatedUtc = now, Status = ProductStatus.Draft };
                _db.Products.Add(product);
            }

            product.Name = request.Name;
            product.Description = request.Description;
            product.BrandId = brand.Id;
            product.Brand = brand;
            product.CategoryId = category.Id;
            product.Category = category;
            product.Season = request.Season;
            product.Gender = request.Gender;
            product.Material = request.Material;
            product.CareInstructions = request.CareInstructions;
            product.VatClass = request.VatClass;
            product.RecommendedPriceEur = request.RecommendedPriceEur;
            product.RecommendedPriceSek = request.RecommendedPriceSek;
            product.UpdatedUtc = now;

            foreach (var s in request.Skus)
            {
                var code = Product.SkuCodeFor(product.StyleCode, s.ColourCode, s.Size);
                var existing = product.Skus.SingleOrDefault(k => k.SkuCode == code);
                if (existing == null)
                {
                    product.AddSku(s.ColourCode, s.ColourName, s.Size, s.Ean, s.CostPrice, s.WeightKg, SortIndex(s.Size));
                }
                else
                {
                    existing.ColourName = s.ColourName;
                    existing.Ean = s.Ean;
                    existing.CostPrice = s.CostPrice;
                    existing.WeightKg = s.WeightKg;
                    existing.Blocked = s.Blocked;
                }
            }

            var images = request.Images ?? new List<string>();
            _db.ProductImages.RemoveRange(product.Images.Where(i => !images.Contains(i.Url)));
            for (var i = 0; i < images.Count; i++)
            {
                if (product.Images.All(x => x.Url != images[i]))
                {
                    product.Images.Add(new ProductImage { Url = images[i], SortOrder = i });
                }
            }

            var wasPublished = product.Status == ProductStatus.Published;
            if (request.Publish && !wasPublished)
            {
                product.Publish(now);
                _outbox.Add(_mapper.Map<ProductPublished>(product));
            }
            else if (wasPublished)
            {
                _outbox.Add(_mapper.Map<ProductUpdated>(product));
            }

            await SaveAsync(cancellationToken);
            await AfterChangeAsync(product, cancellationToken);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> Handle(PublishProduct request, CancellationToken cancellationToken)
        {
            var product = await Load(p => p.StyleCode == request.StyleCode, cancellationToken) ?? throw new NotFoundException("Product", request.StyleCode);
            if (product.Status == ProductStatus.Published) return _mapper.Map<ProductDto>(product);

            product.Publish(DateTime.UtcNow);
            _outbox.Add(_mapper.Map<ProductPublished>(product));
            await SaveAsync(cancellationToken);
            await AfterChangeAsync(product, cancellationToken);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> Handle(DiscontinueProduct request, CancellationToken cancellationToken)
        {
            var product = await Load(p => p.StyleCode == request.StyleCode, cancellationToken) ?? throw new NotFoundException("Product", request.StyleCode);
            var effective = request.EffectiveDate == default ? DateTime.UtcNow.Date : request.EffectiveDate.Date;
            product.Discontinue(effective, DateTime.UtcNow);
            _outbox.Add(new ProductDiscontinued { ProductId = product.Id, StyleCode = product.StyleCode, EffectiveDate = effective });
            await SaveAsync(cancellationToken);
            await AfterChangeAsync(product, cancellationToken);
            return _mapper.Map<ProductDto>(product);
        }

        private Task<Product> Load(System.Linq.Expressions.Expression<Func<Product, bool>> predicate, CancellationToken cancellationToken) =>
            _db.Products.Include(p => p.Brand).Include(p => p.Category).ThenInclude(c => c.Parent)
                .Include(p => p.Skus).Include(p => p.Images)
                .SingleOrDefaultAsync(predicate, cancellationToken);

        private async Task SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("The product was changed by someone else. Reload and try again.");
            }
        }

        private async Task AfterChangeAsync(Product product, CancellationToken cancellationToken)
        {
            await _cache.RemoveAsync("product:" + product.StyleCode.ToUpperInvariant(), cancellationToken);
            await _cache.RemoveAsync("product:" + product.Id.ToString().ToUpperInvariant(), cancellationToken);
            await _search.IndexAsync(ProductDocument.From(product), cancellationToken);
        }

        private static int SortIndex(string size)
        {
            var i = Array.IndexOf(SizeOrder, size?.ToUpperInvariant());
            if (i >= 0) return i;
            return int.TryParse(new string((size ?? string.Empty).TakeWhile(char.IsDigit).ToArray()), out var n) ? 100 + n : 999;
        }
    }
}
