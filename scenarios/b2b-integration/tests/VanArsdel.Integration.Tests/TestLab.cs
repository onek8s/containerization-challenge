using VanArsdel.Integration.LabStubs.Lab;
using VanArsdel.Integration.LabStubs.Seed;

namespace VanArsdel.Integration.Tests
{
    /// <summary>A lab context on a fresh work folder under the temp directory.</summary>
    public sealed class TestLab : IDisposable
    {
        public TestLab(DateTime? now = null)
        {
            Work = Path.Combine(Path.GetTempPath(), "b2b-tests", Guid.NewGuid().ToString("N"));
            Lab = new LabContext(Work, SeedData.Load(ScenarioPaths.Seed), now ?? new DateTime(2026, 3, 2, 9, 0, 0));
        }

        public string Work { get; }
        public LabContext Lab { get; }

        public static byte[] Sample(string name) => File.ReadAllBytes(Path.Combine(ScenarioPaths.Samples, name));

        public static string SampleText(string name) => File.ReadAllText(Path.Combine(ScenarioPaths.Samples, name));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Work, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
