using System.Threading.Tasks;

namespace Fabrikam.Housing.Jobs.Jobs
{
    public interface IHousingJob
    {
        Task RunAsync();
    }
}
