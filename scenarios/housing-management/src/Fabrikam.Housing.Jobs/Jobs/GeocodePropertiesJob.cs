using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.Geo;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Fills in coordinates for homes from their postcode and checks the patch against the GIS patch layer.
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    [DisableConcurrentExecution(1800)]
    public class GeocodePropertiesJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly IPostcodeLookup _postcodes;
        private readonly IPatchLocator _patches;
        private readonly JobsOptions _options;
        private readonly ILogger<GeocodePropertiesJob> _logger;

        public GeocodePropertiesJob(HousingDbContext db, IPostcodeLookup postcodes, IPatchLocator patches, IOptions<JobsOptions> options, ILogger<GeocodePropertiesJob> logger)
        {
            _db = db;
            _postcodes = postcodes;
            _patches = patches;
            _options = options.Value;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var properties = await _db.Properties.Where(p => p.Latitude == null || p.Longitude == null)
                .OrderBy(p => p.Id).Take(_options.GeocodeBatchSize).ToListAsync();
            int located = 0, notFound = 0, patchDifferences = 0;
            foreach (var property in properties)
            {
                try
                {
                    var result = await _postcodes.LookupAsync(property.Postcode);
                    if (result == null)
                    {
                        notFound++;
                        _logger.LogWarning("Postcode {Postcode} for {Property} not found", property.Postcode, property.Reference);
                        continue;
                    }
                    property.Latitude = result.Latitude;
                    property.Longitude = result.Longitude;
                    located++;

                    var patch = await _patches.FindPatchAsync(result.Latitude, result.Longitude);
                    if (patch != null && string.IsNullOrEmpty(property.PatchCode))
                        property.PatchCode = patch;
                    else if (patch != null && !string.Equals(patch, property.PatchCode, StringComparison.OrdinalIgnoreCase))
                    {
                        patchDifferences++;
                        _logger.LogWarning("{Property} is in patch {Recorded} but the GIS layer says {Gis}", property.Reference, property.PatchCode, patch);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Geocoding {Property} failed", property.Reference);
                }
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Geocoding: {Located} located, {NotFound} postcodes not found, {Diff} patch differences", located, notFound, patchDifferences);
        }
    }
}
