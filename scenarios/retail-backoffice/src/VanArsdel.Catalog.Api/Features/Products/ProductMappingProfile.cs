using System;
using System.Linq;
using AutoMapper;
using VanArsdel.Catalog.Api.Domain;
using VanArsdel.Contracts.Catalog;

namespace VanArsdel.Catalog.Api.Features.Products
{
    public class ProductMappingProfile : Profile
    {
        public ProductMappingProfile()
        {
            CreateMap<Sku, SkuDto>();
            CreateMap<Product, ProductDto>()
                .ForMember(d => d.BrandCode, o => o.MapFrom(s => s.Brand.Code))
                .ForMember(d => d.BrandName, o => o.MapFrom(s => s.Brand.Name))
                .ForMember(d => d.CategoryCode, o => o.MapFrom(s => s.Category.Code))
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category.Name))
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.RowVersion, o => o.MapFrom(s => s.RowVersion == null ? null : Convert.ToBase64String(s.RowVersion)))
                .ForMember(d => d.Skus, o => o.MapFrom(s => s.Skus.OrderBy(x => x.ColourCode).ThenBy(x => x.SortOrder)))
                .ForMember(d => d.Images, o => o.MapFrom(s => s.Images.OrderBy(i => i.SortOrder).Select(i => i.Url)));
            CreateMap<Product, ProductSummaryDto>()
                .ForMember(d => d.Brand, o => o.MapFrom(s => s.Brand.Name))
                .ForMember(d => d.Category, o => o.MapFrom(s => s.Category.Code))
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.SkuCount, o => o.MapFrom(s => s.Skus.Count));

            CreateMap<Sku, SkuInfo>()
                .ForMember(d => d.Sku, o => o.MapFrom(s => s.SkuCode))
                .ForMember(d => d.Colour, o => o.MapFrom(s => s.ColourName));
            CreateMap<Product, ProductPublished>()
                .ForMember(d => d.ProductId, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.Brand, o => o.MapFrom(s => s.Brand.Name))
                .ForMember(d => d.CategoryCode, o => o.MapFrom(s => s.Category.Code))
                .ForMember(d => d.Skus, o => o.MapFrom(s => s.Skus.Where(x => !x.Blocked)))
                .ForMember(d => d.PublishedUtc, o => o.MapFrom(s => s.PublishedUtc ?? s.UpdatedUtc));
            CreateMap<Product, ProductUpdated>()
                .ForMember(d => d.ProductId, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.CategoryCode, o => o.MapFrom(s => s.Category.Code))
                .ForMember(d => d.Skus, o => o.MapFrom(s => s.Skus.Where(x => !x.Blocked)));
        }
    }
}
