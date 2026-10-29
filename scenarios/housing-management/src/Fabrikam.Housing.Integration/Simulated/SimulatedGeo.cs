using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Integration.Geo;

namespace Fabrikam.Housing.Integration.Simulated
{
    /// <summary>Any valid postcode resolves to a stable point near central Bristol.</summary>
    public class SimulatedPostcodeLookup : IPostcodeLookup
    {
        public Task<PostcodeResult> LookupAsync(string postcode)
        {
            if (!Postcode.TryNormalise(postcode, out var normalised)) return Task.FromResult<PostcodeResult>(null);
            var hash = 0;
            foreach (var c in normalised) hash = unchecked(hash * 31 + c);
            var dx = (Math.Abs(hash) % 1000) / 1000.0 - 0.5;
            var dy = (Math.Abs(hash / 1000) % 1000) / 1000.0 - 0.5;
            return Task.FromResult(new PostcodeResult
            {
                Postcode = normalised,
                Latitude = Math.Round(51.4545 + dy * 0.08, 6),
                Longitude = Math.Round(-2.5879 + dx * 0.12, 6),
                AdminDistrict = "Bristol, City of",
                Ward = "Simulated ward " + Postcode.Outward(normalised)
            });
        }
    }

    /// <summary>
    /// Maps points back to patches by the outward code of the nearest known district.
    /// Development data only uses BS3, BS4, BS5, BS13, BS14 and BS16.
    /// </summary>
    public class SimulatedPatchLocator : IPatchLocator
    {
        public static readonly IDictionary<string, string> PatchByOutward = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BS5"] = "EAST", ["BS3"] = "EAST",
            ["BS13"] = "SOUTH", ["BS14"] = "SOUTH",
            ["BS16"] = "NORTH", ["BS4"] = "NORTH"
        };

        private static readonly (string Outward, double Lat, double Lon)[] Centres =
        {
            ("BS5", 51.4652, -2.5634), ("BS3", 51.4420, -2.6010), ("BS13", 51.4153, -2.6170),
            ("BS14", 51.4140, -2.5600), ("BS16", 51.4870, -2.5170), ("BS4", 51.4470, -2.5480)
        };

        public static string ForPostcode(string postcode)
        {
            var outward = Postcode.Outward(postcode);
            return outward != null && PatchByOutward.TryGetValue(outward, out var patch) ? patch : null;
        }

        public Task<string> FindPatchAsync(double latitude, double longitude)
        {
            string best = null;
            var bestDistance = double.MaxValue;
            foreach (var c in Centres)
            {
                var d = Math.Pow(c.Lat - latitude, 2) + Math.Pow((c.Lon - longitude) * 0.62, 2);
                if (d < bestDistance) { bestDistance = d; best = c.Outward; }
            }
            // About 5 km from the nearest district centre is outside Fabrikam's area.
            return Task.FromResult(bestDistance > 0.0025 ? null : PatchByOutward[best]);
        }
    }
}
