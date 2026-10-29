using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fabrikam.Housing.Integration.Bacs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Integration.Simulated
{
    public class SimulatedBacsBureau : IBacsBureau
    {
        private readonly string _folder;
        private readonly ILogger<SimulatedBacsBureau> _logger;

        public SimulatedBacsBureau(IOptions<IntegrationOptions> options, ILogger<SimulatedBacsBureau> logger)
        {
            _folder = SimulatorFolder.Ensure(options.Value.SimulatorFolder, "bacs");
            _logger = logger;
        }

        public Task UploadAsync(string fileName, string content)
        {
            var path = Path.Combine(_folder, SimulatorFolder.Safe(fileName));
            File.WriteAllText(path, content, Encoding.ASCII);
            _logger.LogInformation("Simulated Bacs upload written to {Path}", path);
            return Task.CompletedTask;
        }
    }
}
