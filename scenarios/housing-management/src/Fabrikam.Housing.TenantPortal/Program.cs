using Fabrikam.Housing.Hosting;

namespace Fabrikam.Housing.TenantPortal
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return HousingHost.Run<Startup>(args, "TenantPortal");
        }
    }
}
