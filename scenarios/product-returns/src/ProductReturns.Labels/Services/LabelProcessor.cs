using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductReturns.Core;
using ProductReturns.Data;
using ProductReturns.Labels.Carriers;

namespace ProductReturns.Labels.Services
{
    /// <summary>Books return labels for approved returns and mails them to the customer.</summary>
    public class LabelProcessor
    {
        private readonly ReturnsRepository _repository;
        private readonly ICarrierClient _carrier;
        private readonly LabelStore _store;
        private readonly LabelMailer _mailer;
        private readonly CarrierQuota _quota;
        private readonly LabelOptions _options;
        private readonly ILogger<LabelProcessor> _logger;

        public LabelProcessor(ReturnsRepository repository, ICarrierClient carrier, LabelStore store, LabelMailer mailer,
            CarrierQuota quota, IOptions<LabelOptions> options, ILogger<LabelProcessor> logger)
        {
            _repository = repository;
            _carrier = carrier;
            _store = store;
            _mailer = mailer;
            _quota = quota;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<int> RunOnceAsync(CancellationToken ct)
        {
            var pending = await _repository.PendingLabelsAsync(_options.MaxAttempts, _options.BatchSize, ct);
            var done = 0;
            foreach (var request in pending)
            {
                if (!_quota.TryTake())
                {
                    _logger.LogWarning("Daily carrier quota of {Quota} labels reached, {Left} returns wait until tomorrow",
                        _options.DailyQuota, pending.Count - done);
                    break;
                }

                if (await ProcessAsync(request, ct))
                {
                    done++;
                }
            }

            return done;
        }

        private async Task<bool> ProcessAsync(ReturnRequest request, CancellationToken ct)
        {
            CarrierLabel label;
            try
            {
                label = await _carrier.CreateReturnLabelAsync(await BuildRequestAsync(request, ct), ct);
            }
            catch (Exception ex) when (ex is CarrierException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                _quota.Release();
                request.LabelAttempts++;
                request.LabelError = ex.Message.Length > 1000 ? ex.Message.Substring(0, 1000) : ex.Message;
                if (ex is CarrierException carrierException && carrierException.Permanent)
                {
                    request.LabelAttempts = _options.MaxAttempts;
                }

                await _repository.SaveAsync(ct);
                _logger.LogError(ex, "Label for {Rma} failed (attempt {Attempt})", request.RmaNumber, request.LabelAttempts);
                return false;
            }

            var now = DateTime.UtcNow;
            request.LabelPath = _store.Save(request.RmaNumber, now, label.Pdf);
            request.TrackingNumber = label.TrackingNumber;
            request.LabelCreatedUtc = now;
            request.LabelError = null;
            request.LabelAttempts++;
            request.Status = ReturnStatus.LabelIssued;
            request.Notes.Add(new ReturnNote { Author = "labels", Text = "Return label " + label.TrackingNumber + " created.", CreatedUtc = now });
            await _repository.SaveAsync(ct);

            try
            {
                await _mailer.SendLabelAsync(request, label.TrackingNumber, label.Pdf);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Label e-mail for {Rma} to {Email} failed", request.RmaNumber, request.CustomerEmail);
                request.Notes.Add(new ReturnNote { Author = "labels", Text = "Label e-mail could not be sent: " + ex.Message, CreatedUtc = DateTime.UtcNow });
                await _repository.SaveAsync(ct);
            }

            _logger.LogInformation("Return {Rma} label {Tracking} issued", request.RmaNumber, label.TrackingNumber);
            return true;
        }

        private async Task<LabelRequest> BuildRequestAsync(ReturnRequest request, CancellationToken ct)
        {
            var order = await _repository.FindOrderAsync(request.OrderNumber, ct);
            var skus = request.Lines.Select(l => l.Sku).Distinct().ToList();
            var weights = await _repository.Db.Catalog.AsNoTracking()
                .Where(c => skus.Contains(c.Sku))
                .ToDictionaryAsync(c => c.Sku, c => c.WeightKg, ct);

            var weight = request.Lines.Sum(l => l.Quantity * (weights.TryGetValue(l.Sku, out var w) ? w : 1m));
            return new LabelRequest
            {
                Reference = request.RmaNumber,
                CustomerEmail = request.CustomerEmail,
                WeightKg = weight,
                From = new Address
                {
                    Name = order?.ShipToName ?? request.CustomerName,
                    Street = order?.ShipToStreet,
                    PostalCode = order?.ShipToPostalCode,
                    City = order?.ShipToCity,
                    Country = order?.ShipToCountry
                },
                To = new Address
                {
                    Name = _options.WarehouseName,
                    Street = _options.WarehouseStreet,
                    PostalCode = _options.WarehousePostalCode,
                    City = _options.WarehouseCity,
                    Country = _options.WarehouseCountry
                }
            };
        }
    }
}
