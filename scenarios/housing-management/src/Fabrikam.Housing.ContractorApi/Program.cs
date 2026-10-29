using Fabrikam.Housing.Hosting;
using Microsoft.Extensions.Hosting;

namespace Fabrikam.Housing.ContractorApi
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return HousingHost.Run<Startup>(args, "ContractorApi");
        }

        public static IHostBuilder CreateHostBuilder(string[] args) => HousingHost.CreateHostBuilder<Startup>(args);
    }
}
