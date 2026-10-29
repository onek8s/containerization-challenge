using System.Threading.Tasks;

namespace Fabrikam.Housing.Integration.Bacs
{
    public interface IBacsBureau
    {
        /// <summary>Uploads a Standard 18 file to the bureau's inbound folder.</summary>
        Task UploadAsync(string fileName, string content);
    }
}
