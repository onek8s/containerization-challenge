using System.Threading;
using System.Threading.Tasks;

namespace ProductReturns.Labels.Carriers
{
    public class Address
    {
        public string Name { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }

    public class LabelRequest
    {
        public string Reference { get; set; }
        public Address From { get; set; }
        public Address To { get; set; }
        public decimal WeightKg { get; set; }
        public string CustomerEmail { get; set; }
    }

    public class CarrierLabel
    {
        public string TrackingNumber { get; set; }
        public byte[] Pdf { get; set; }
    }

    public class CarrierException : System.Exception
    {
        public CarrierException(string message, bool permanent = false)
            : base(message)
        {
            Permanent = permanent;
        }

        /// <summary>The carrier rejected the request; retrying will not help.</summary>
        public bool Permanent { get; }
    }

    public interface ICarrierClient
    {
        Task<CarrierLabel> CreateReturnLabelAsync(LabelRequest request, CancellationToken cancellationToken);
    }
}
