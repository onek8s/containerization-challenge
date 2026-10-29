using System;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VanArsdel.ErpConnector.Data;
using VanArsdel.Integration.Erp;

namespace VanArsdel.ErpConnector
{
    public class ErpPostingException : Exception
    {
        public ErpPostingException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Calls the ERP once per document. Retryable faults are rethrown so MassTransit retries the message;
    /// business faults are logged in ErpPosting for finance support and the message is acknowledged.
    /// </summary>
    public class ErpGateway
    {
        private readonly ErpDbContext _db;
        private readonly ErpClientSettings _settings;
        private readonly ErpCredentials _credentials;
        private readonly ILogger<ErpGateway> _logger;

        public ErpGateway(ErpDbContext db, IOptions<ErpClientSettings> settings, ErpCredentials credentials, ILogger<ErpGateway> logger)
        {
            _db = db;
            _settings = settings.Value;
            _credentials = credentials;
            _logger = logger;
        }

        public ErpClientSettings Settings => _settings;

        public async Task PostOnceAsync(string kind, string externalReference, Func<IErpIntegration, Task<ErpResult>> call)
        {
            var posting = await _db.Postings.SingleOrDefaultAsync(p => p.Kind == kind && p.ExternalReference == externalReference);
            if (posting?.Status == PostingStatus.Posted)
            {
                _logger.LogInformation("{Kind} {Reference} already posted as {DocumentNo}", kind, externalReference, posting.DocumentNo);
                return;
            }
            if (posting == null)
            {
                posting = new ErpPosting { Kind = kind, ExternalReference = externalReference, CreatedUtc = DateTime.UtcNow };
                _db.Postings.Add(posting);
            }
            posting.Attempts++;
            posting.UpdatedUtc = DateTime.UtcNow;

            try
            {
                var result = await CallAsync(call);
                posting.Status = result.Success ? PostingStatus.Posted : PostingStatus.Failed;
                posting.DocumentNo = result.DocumentNo;
                posting.Error = result.Success ? null : Truncate(result.Message);
                if (result.Success)
                {
                    _logger.LogInformation("{Kind} {Reference} posted as {DocumentNo}", kind, externalReference, result.DocumentNo);
                }
                else
                {
                    _logger.LogWarning("ERP rejected {Kind} {Reference}: {Message}", kind, externalReference, result.Message);
                }
            }
            catch (Exception ex) when (ErpClientFactory.IsRetryable(ex))
            {
                posting.Status = PostingStatus.Failed;
                posting.Error = Truncate(ex.Message);
                await _db.SaveChangesAsync();
                throw new ErpPostingException($"ERP unavailable for {kind} {externalReference}", ex);
            }
            catch (FaultException ex)
            {
                posting.Status = PostingStatus.Failed;
                posting.Error = Truncate(ex.Message);
                _logger.LogError(ex, "ERP fault for {Kind} {Reference}", kind, externalReference);
            }
            await _db.SaveChangesAsync();
        }

        public async Task<T> CallAsync<T>(Func<IErpIntegration, Task<T>> call)
        {
            var client = ErpClientFactory.Create(_settings, _credentials.Password);
            try
            {
                var result = await call(client);
                await client.CloseAsync();
                return result;
            }
            catch
            {
                client.Abort();
                throw;
            }
        }

        public string ItemNo(string styleCode) => _settings.ItemPrefix + styleCode;

        /// <summary>SKU "VA1001-BLK-M" becomes variant "BLK-M" of item "VAVA1001".</summary>
        public (string ItemNo, string Variant) Split(string sku)
        {
            var parts = sku.Split('-');
            return parts.Length < 2 ? (ItemNo(sku), "") : (ItemNo(parts[0]), string.Join("-", parts.Skip(1)));
        }

        private static string Truncate(string value) => value == null || value.Length <= 1000 ? value : value.Substring(0, 1000);
    }

    public class ErpCredentials
    {
        public ErpCredentials(string password)
        {
            Password = password;
        }

        public string Password { get; }
    }
}
