using Fabrikam.Housing.Hosting;
using Microsoft.Extensions.Hosting;

namespace Fabrikam.Housing.Jobs
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return HousingHost.Run<Startup>(args, "Jobs");
        }

        public static IHostBuilder CreateHostBuilder(string[] args) => HousingHost.CreateHostBuilder<Startup>(args);
    }
}
