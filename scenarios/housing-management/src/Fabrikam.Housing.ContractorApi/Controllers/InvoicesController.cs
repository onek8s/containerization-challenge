using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.ContractorApi.Infrastructure;
using Fabrikam.Housing.ContractorApi.Models;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.ContractorApi.Controllers
{
    [ApiController]
    [Route("api/v1/invoices")]
    [Authorize(Policy = Startup.ContractorPolicy)]
    public class InvoicesController : ControllerBase
    {
        private readonly HousingDbContext _db;
        private readonly CurrentContractor _contractor;
        private readonly IClock _clock;
        private readonly AuditWriter _audit;
        private readonly IMapper _mapper;

        public InvoicesController(HousingDbContext db, CurrentContractor contractor, IClock clock, AuditWriter audit, IMapper mapper)
        {
            _db = db;
            _contractor = contractor;
            _clock = clock;
            _audit = audit;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IList<InvoiceModel>> List(InvoiceStatus? status)
        {
            var contractor = await _contractor.GetAsync();
            var query = _db.ContractorInvoices.AsNoTracking().Include(i => i.WorkOrder).Where(i => i.ContractorId == contractor.Id);
            if (status.HasValue) query = query.Where(i => i.Status == status.Value);
            var invoices = await query.OrderByDescending(i => i.InvoiceDate).Take(500).ToListAsync();
            return _mapper.Map<IList<InvoiceModel>>(invoices);
        }

        [HttpGet("{invoiceNumber}")]
        public async Task<ActionResult<InvoiceModel>> Get(string invoiceNumber)
        {
            var contractor = await _contractor.GetAsync();
            var invoice = await _db.ContractorInvoices.AsNoTracking().Include(i => i.WorkOrder)
                .SingleOrDefaultAsync(i => i.ContractorId == contractor.Id && i.InvoiceNumber == invoiceNumber);
            if (invoice == null) return NotFound();
            return _mapper.Map<InvoiceModel>(invoice);
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceModel>> Submit(InvoiceRequest request)
        {
            var contractor = await _contractor.GetAsync();
            var number = request.InvoiceNumber.Trim();
            if (await _db.ContractorInvoices.AnyAsync(i => i.ContractorId == contractor.Id && i.InvoiceNumber == number))
                return Conflict(new ProblemDetails { Status = 409, Title = "Duplicate invoice", Detail = "Invoice " + number + " has already been submitted." });

            var order = await _db.WorkOrders.Include(w => w.Lines)
                .SingleOrDefaultAsync(w => w.Reference == request.WorkOrderReference && w.ContractorId == contractor.Id);
            if (order == null)
                throw new BusinessRuleException("INV_WORKORDER", "Work order " + request.WorkOrderReference + " is not assigned to you.");
            if (await _db.ContractorInvoices.AnyAsync(i => i.WorkOrderId == order.Id && i.Status != InvoiceStatus.Rejected))
                throw new BusinessRuleException("INV_DUPLICATE_ORDER", "Work order " + order.Reference + " has already been invoiced.");

            var invoice = new ContractorInvoice
            {
                ContractorId = contractor.Id,
                InvoiceNumber = number,
                InvoiceDate = request.InvoiceDate.Date,
                WorkOrderId = order.Id,
                WorkOrder = order,
                NetAmount = Money.Round(request.NetAmount),
                VatAmount = Money.Round(request.VatAmount),
                CreatedAt = _clock.Now,
                CreatedBy = _contractor.ClientId
            };
            var problem = InvoiceMatching.Check(invoice, order, contractor);
            invoice.Status = problem == null ? InvoiceStatus.Received : InvoiceStatus.Queried;
            invoice.QueryReason = problem;
            _db.ContractorInvoices.Add(invoice);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(_contractor.ClientId, "ContractorInvoice", contractor.Code + "/" + number, "Submitted",
                new { order.Reference, invoice.NetAmount, invoice.VatAmount, invoice.Status, invoice.QueryReason });

            var model = _mapper.Map<InvoiceModel>(invoice);
            return CreatedAtAction(nameof(Get), new { invoiceNumber = number }, model);
        }
    }
}
