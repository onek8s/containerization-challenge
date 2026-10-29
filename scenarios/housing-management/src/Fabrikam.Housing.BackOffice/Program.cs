using Fabrikam.Housing.Hosting;
using Microsoft.Extensions.Hosting;

namespace Fabrikam.Housing.BackOffice
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return HousingHost.Run<Startup>(args, "BackOffice");
        }

        public static IHostBuilder CreateHostBuilder(string[] args) => HousingHost.CreateHostBuilder<Startup>(args);
    }
}
