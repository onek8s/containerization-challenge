using AutoMapper;
using Lucerne.Lending.Contracts;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductRepository _products;
    private readonly ICoreReferenceData _referenceData;
    private readonly IMapper _mapper;

    public ProductsController(ProductRepository products, ICoreReferenceData referenceData, IMapper mapper)
    {
        _products = products;
        _referenceData = referenceData;
        _mapper = mapper;
    }

    /// <summary>Products open for sale with the indicative rate for risk grade B.</summary>
    [HttpGet]
    public async Task<IEnumerable<ProductDto>> List(CancellationToken ct)
    {
        var date = await _referenceData.GetBusinessDateAsync(ct);
        var result = new List<ProductDto>();
        foreach (var p in await _products.ListAsync(true, ct))
        {
            var dto = _mapper.Map<ProductDto>(p);
            var refRate = await _referenceData.GetReferenceRateAsync(ScoringService.ReferenceRateCode(p.ReferenceRate), date, ct);
            dto.IndicativeRatePercent = Pricing.Quote(p, p.MinAmount, p.MinTermMonths, RiskGrade.B, refRate).NominalRatePercent;
            result.Add(dto);
        }
        return result;
    }

    /// <summary>Loan calculator with the representative example required by the Consumer Protection Act 7:8.</summary>
    [HttpPost("calculate")]
    public async Task<ActionResult<CalculatorResponse>> Calculate(CalculatorRequest request, CancellationToken ct)
    {
        var product = await _products.GetAsync(request.ProductCode, ct);
        if (product is not { Active: true }) return NotFound();
        var errors = product.Validate(request.Amount, request.TermMonths, product.Purposes.FirstOrDefault()).ToList();
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = errors.ToArray() }));
        var date = await _referenceData.GetBusinessDateAsync(ct);
        var refRate = await _referenceData.GetReferenceRateAsync(ScoringService.ReferenceRateCode(product.ReferenceRate), date, ct);
        var q = Pricing.Quote(product, request.Amount, request.TermMonths, RiskGrade.B, refRate);
        return new CalculatorResponse
        {
            NominalRatePercent = q.NominalRatePercent,
            AprPercent = q.AprPercent,
            MonthlyPayment = q.MonthlyPayment,
            MonthlyFee = q.MonthlyFee,
            ArrangementFee = q.ArrangementFee,
            TotalRepayable = q.TotalRepayable,
            Example = $"Example: {request.Amount:0} EUR over {request.TermMonths} months, nominal rate {q.NominalRatePercent:0.00} % " +
                      $"({(product.ReferenceRate == ReferenceRateType.Fixed ? "fixed" : "12 month Euribor " + q.ReferenceRatePercent.ToString("0.000") + " % + margin " + q.MarginPercent.ToString("0.00") + " %")}), " +
                      $"arrangement fee {q.ArrangementFee:0.00} EUR, monthly fee {q.MonthlyFee:0.00} EUR, APR {q.AprPercent:0.00} %, " +
                      $"total {q.TotalRepayable:0.00} EUR. Final rate depends on the credit decision."
        };
    }
}
