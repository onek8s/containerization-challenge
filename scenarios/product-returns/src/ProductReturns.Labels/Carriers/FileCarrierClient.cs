using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProductReturns.Labels.Carriers
{
    /// <summary>Development carrier: creates a plain PDF label without calling the carrier.</summary>
    public class FileCarrierClient : ICarrierClient
    {
        public Task<CarrierLabel> CreateReturnLabelAsync(LabelRequest request, CancellationToken cancellationToken)
        {
            var tracking = "DEV" + DateTime.UtcNow.ToString("yyMMddHHmmss") + Math.Abs(request.Reference.GetHashCode() % 1000).ToString("D3");
            var pdf = SimplePdf.Create(new[]
            {
                "RETURN LABEL - DEVELOPMENT",
                "Tracking: " + tracking,
                "Reference: " + request.Reference,
                string.Empty,
                "From: " + request.From.Name,
                request.From.Street,
                request.From.PostalCode + " " + request.From.City + " " + request.From.Country,
                string.Empty,
                "To: " + request.To.Name,
                request.To.Street,
                request.To.PostalCode + " " + request.To.City + " " + request.To.Country,
                string.Empty,
                "Weight: " + request.WeightKg.ToString("0.0") + " kg"
            });

            return Task.FromResult(new CarrierLabel { TrackingNumber = tracking, Pdf = pdf });
        }
    }
}
