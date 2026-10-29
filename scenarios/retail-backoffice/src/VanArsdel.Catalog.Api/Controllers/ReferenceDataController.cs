using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanArsdel.Catalog.Api.Data;
using VanArsdel.Catalog.Api.Domain;

namespace VanArsdel.Catalog.Api.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize(Policy = Policies.Read)]
    public class ReferenceDataController : ControllerBase
    {
        private readonly CatalogDbContext _db;

        public ReferenceDataController(CatalogDbContext db)
        {
            _db = db;
        }

        [HttpGet("brands")]
        public async Task<IActionResult> Brands() =>
            Ok(await _db.Brands.AsNoTracking().OrderBy(b => b.Name).Select(b => new { b.Code, b.Name, b.OwnBrand }).ToListAsync());

        [HttpGet("categories")]
        public async Task<IActionResult> Categories()
        {
            var all = await _db.Categories.AsNoTracking().ToListAsync();
            var tree = all.Where(c => c.ParentId == null).OrderBy(c => c.Name).Select(r => new
            {
                r.Code,
                r.Name,
                Children = all.Where(c => c.ParentId == r.Id).OrderBy(c => c.Name)
                    .Select(c => new { c.Code, c.Name, c.ErpItemGroup, c.SizeScale }),
            });
            return Ok(tree);
        }

        [HttpPost("brands")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> AddBrand([FromBody] Brand brand)
        {
            if (string.IsNullOrWhiteSpace(brand.Code) || string.IsNullOrWhiteSpace(brand.Name)) return BadRequest();
            if (await _db.Brands.AnyAsync(b => b.Code == brand.Code)) return Conflict();
            brand.Id = 0;
            _db.Brands.Add(brand);
            await _db.SaveChangesAsync();
            return Ok(new { brand.Code, brand.Name, brand.OwnBrand });
        }
    }
}
