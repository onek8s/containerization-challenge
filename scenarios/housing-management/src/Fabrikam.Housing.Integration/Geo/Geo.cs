using System.Threading.Tasks;

namespace Fabrikam.Housing.Integration.Geo
{
    public class PostcodeResult
    {
        public string Postcode { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string AdminDistrict { get; set; }
        public string Ward { get; set; }
    }

    public interface IPostcodeLookup
    {
        /// <summary>Returns null when the postcode does not exist.</summary>
        Task<PostcodeResult> LookupAsync(string postcode);
    }

    public interface IPatchLocator
    {
        /// <summary>Housing patch code containing the point, or null when outside all patches.</summary>
        Task<string> FindPatchAsync(double latitude, double longitude);
    }
}
