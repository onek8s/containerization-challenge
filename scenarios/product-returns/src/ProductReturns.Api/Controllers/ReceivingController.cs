using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductReturns.Api.Security;
using ProductReturns.Api.Services;
using ProductReturns.Core;
using ProductReturns.Core.Api;

namespace ProductReturns.Api.Controllers
{
    /// <summary>Warehouse handheld scanners at the returns dock.</summary>
    [Route("api/receiving")]
    [Authorize(Policies.Scanner)]
    public class ReceivingController : ApiControllerBase
    {
        private readonly ReturnService _returns;

        public ReceivingController(ReturnService returns)
        {
            _returns = returns;
        }

        [HttpGet("{rma}")]
        public Task<IActionResult> Expected(string rma, CancellationToken ct) => Run(async () =>
        {
            if (!RmaNumber.IsValid(rma))
            {
                return Problem(detail: "Not a valid return number.", statusCode: 400);
            }

            var request = await _returns.RequireAsync(rma, ct);
            return Ok(new ScanResponse
            {
                RmaNumber = request.RmaNumber,
                Accepted = ReceivingRules.CanReceive(request.Status),
                Complete = request.Lines.All(l => l.ReceivedQuantity >= l.Quantity),
                Message = request.Status.ToString(),
                Lines = request.Lines.Select(l => l.ToDetail()).ToList()
            });
        });

        [HttpPost("{rma}/scan")]
        public Task<IActionResult> Scan(string rma, [FromBody] ScanRequest scan, CancellationToken ct) => Run(async () =>
        {
            if (!RmaNumber.IsValid(rma))
            {
                return Problem(detail: "Not a valid return number.", statusCode: 400);
            }

            var (request, result) = await _returns.ScanAsync(rma, User.Identity.Name, scan, ct);
            var response = new ScanResponse
            {
                RmaNumber = request.RmaNumber,
                Accepted = result.Accepted,
                Complete = result.Complete,
                Message = result.Message,
                Lines = request.Lines.Select(l => l.ToDetail()).ToList()
            };
            return result.Accepted ? Ok(response) : (IActionResult)UnprocessableEntity(response);
        });
    }
}
