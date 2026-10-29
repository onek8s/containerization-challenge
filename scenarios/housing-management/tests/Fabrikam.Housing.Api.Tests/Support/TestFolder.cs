using System;
using System.IO;

namespace Fabrikam.Housing.Api.Tests.Support
{
    public sealed class TestFolder : IDisposable
    {
        public TestFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "housing-api-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Sub(params string[] parts) => System.IO.Path.Combine(Path, System.IO.Path.Combine(parts));

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
