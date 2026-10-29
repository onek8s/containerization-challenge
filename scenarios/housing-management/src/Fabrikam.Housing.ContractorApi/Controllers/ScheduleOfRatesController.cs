using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.ContractorApi.Models;
using Fabrikam.Housing.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.ContractorApi.Controllers
{
    [ApiController]
    [Route("api/v1/schedule-of-rates")]
    [Authorize(Policy = Startup.ContractorPolicy)]
    public class ScheduleOfRatesController : ControllerBase
    {
        private readonly HousingDbContext _db;
        private readonly IMapper _mapper;

        public ScheduleOfRatesController(HousingDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        [HttpGet]
        [ResponseCache(Duration = 3600)]
        public async Task<IList<ScheduleOfRatesModel>> Get()
        {
            var items = await _db.ScheduleOfRates.AsNoTracking().Where(s => s.Active).OrderBy(s => s.Code).ToListAsync();
            return _mapper.Map<IList<ScheduleOfRatesModel>>(items);
        }
    }
}
