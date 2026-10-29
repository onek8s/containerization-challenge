using System.Threading;
using System.Threading.Tasks;

namespace ProductReturns.Jobs.Jobs
{
    public interface IJob
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}
