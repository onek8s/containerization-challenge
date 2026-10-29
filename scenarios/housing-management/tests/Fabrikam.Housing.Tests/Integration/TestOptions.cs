using System;
using System.IO;
using Fabrikam.Housing.Integration;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Tests.Integration
{
    public sealed class SimulatorFolderFixture : IDisposable
    {
        public SimulatorFolderFixture()
        {
            Folder = Path.Combine(Path.GetTempPath(), "housing-sim-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
        }

        public string Folder { get; }

        public IOptions<IntegrationOptions> Options(Action<IntegrationOptions> configure = null)
        {
            var o = new IntegrationOptions { Mode = IntegrationMode.Simulated, SimulatorFolder = Folder };
            configure?.Invoke(o);
            return Microsoft.Extensions.Options.Options.Create(o);
        }

        public void Dispose()
        {
            try { Directory.Delete(Folder, true); } catch (IOException) { }
        }
    }
}
