using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductReturns.Api.Security;
using ProductReturns.Api.Services;
using ProductReturns.Core;
using ProductReturns.Core.Api;
using ProductReturns.Data;

namespace ProductReturns.Api.Controllers
{
    [Route("api/returns")]
    [Authorize(Policies.Portal)]
    public class ReturnsController : ApiControllerBase
    {
        private readonly ReturnService _returns;
        private readonly ReturnsRepository _repository;

        public ReturnsController(ReturnService returns, ReturnsRepository repository)
        {
            _returns = returns;
            _repository = repository;
        }

        [HttpPost]
        public Task<IActionResult> Create([FromBody] CreateReturnRequest request, CancellationToken ct) => Run(async () =>
        {
            var created = await _returns.CreateAsync(request, Actor, ct);
            return CreatedAtAction(nameof(Get), new { rma = created.RmaNumber },
                new CreateReturnResponse { RmaNumber = created.RmaNumber, Status = created.Status });
        });

        [HttpGet]
        public async Task<ReturnSearchResponse> Search([FromQuery] ReturnStatus? status, [FromQuery] string q,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        {
            var result = await _repository.SearchAsync(new ReturnSearch { Status = status, Text = q, Page = page, PageSize = pageSize }, ct);
            return new ReturnSearchResponse
            {
                Items = ReturnService.Summaries(result.Items).ToList(),
                Total = result.Total,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary(CancellationToken ct)
        {
            var counts = await _repository.CountByStatusAsync(ct);
            return Ok(Enum.GetValues(typeof(ReturnStatus)).Cast<ReturnStatus>()
                .ToDictionary(s => s.ToString(), s => counts.TryGetValue(s, out var c) ? c : 0));
        }

        /// <summary>Return details. With <paramref name="email"/> the caller is the customer and the address must match.</summary>
        [HttpGet("{rma}")]
        public async Task<ActionResult<ReturnDetail>> Get(string rma, [FromQuery] string email, CancellationToken ct)
        {
            var request = await _repository.GetAsync(rma, ct);
            if (request == null || (email != null && !ReturnService.SameEmail(request.CustomerEmail, email)))
            {
                return NotFound();
            }

            var detail = request.ToDetail();
            if (email != null)
            {
                detail.Notes.Clear();
                detail.DecidedBy = null;
                detail.InspectedBy = null;
            }

            return detail;
        }

        [HttpGet("{rma}/label")]
        public async Task<IActionResult> Label(string rma, [FromQuery] string email, CancellationToken ct)
        {
            var request = await _repository.GetAsync(rma, ct);
            if (request == null || (email != null && !ReturnService.SameEmail(request.CustomerEmail, email))
                || string.IsNullOrEmpty(request.LabelPath) || !System.IO.File.Exists(request.LabelPath))
            {
                return NotFound();
            }

            return PhysicalFile(request.LabelPath, "application/pdf", request.RmaNumber + ".pdf");
        }

        [HttpPost("{rma}/approve")]
        public Task<IActionResult> Approve(string rma, CancellationToken ct) => Run(async () =>
            Ok((await _returns.ApproveAsync(rma, Actor, ct)).ToDetail()));

        [HttpPost("{rma}/reject")]
        public Task<IActionResult> Reject(string rma, [FromBody] DecisionRequest request, CancellationToken ct) => Run(async () =>
            Ok((await _returns.RejectAsync(rma, Actor, request?.Reason, ct)).ToDetail()));

        [HttpPost("{rma}/cancel")]
        public Task<IActionResult> Cancel(string rma, [FromQuery] string email, CancellationToken ct) => Run(async () =>
            Ok((await _returns.CancelAsync(rma, email, ct)).ToDetail()));

        [HttpPost("{rma}/notes")]
        public Task<IActionResult> AddNote(string rma, [FromBody] NoteRequest request, CancellationToken ct) => Run(async () =>
            Ok((await _returns.AddNoteAsync(rma, Actor, request?.Text, ct)).ToDetail()));

        [HttpPost("{rma}/complete-inspection")]
        public Task<IActionResult> CompleteInspection(string rma, CancellationToken ct) => Run(async () =>
            Ok((await _returns.CompleteInspectionAsync(rma, Actor, ct)).ToDetail()));
    }
}
