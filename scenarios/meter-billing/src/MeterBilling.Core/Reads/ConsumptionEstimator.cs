using System;

namespace Coho.Billing.Core.Reads
{
    /// <summary>
    /// Estimates a closing reading from the account's average daily use with a monthly
    /// seasonal profile (garden watering in summer).
    /// </summary>
    public class ConsumptionEstimator
    {
        private static readonly decimal[] DefaultProfile =
        {
            0.92m, 0.92m, 0.94m, 0.97m, 1.05m, 1.15m, 1.20m, 1.14m, 1.01m, 0.95m, 0.92m, 0.93m
        };

        private readonly decimal[] _profile;

        public ConsumptionEstimator()
            : this(DefaultProfile)
        {
        }

        public ConsumptionEstimator(decimal[] monthlyProfile)
        {
            if (monthlyProfile == null || monthlyProfile.Length != 12)
            {
                throw new ArgumentException("Profile must have twelve monthly factors", "monthlyProfile");
            }

            _profile = monthlyProfile;
        }

        public decimal EstimateConsumption(decimal averageDailyUse, DateTime from, DateTime to)
        {
            var total = 0m;
            for (var day = from.Date; day < to.Date; day = day.AddDays(1))
            {
                total += averageDailyUse * _profile[day.Month - 1];
            }

            // Estimates are whole cubic metres so that the next actual read corrects them.
            return Math.Round(total, 0, MidpointRounding.AwayFromZero);
        }

        public MeterRead Estimate(BillingAccount account, MeterRead previous, DateTime readDate)
        {
            if (account == null) throw new ArgumentNullException("account");
            if (previous == null) throw new ArgumentNullException("previous");

            var multiplier = account.Meter != null && account.Meter.Multiplier > 0 ? account.Meter.Multiplier : 1m;
            var consumption = EstimateConsumption(account.AverageDailyUse, previous.ReadDate, readDate);

            var dials = account.Meter != null && account.Meter.Dials > 0 ? account.Meter.Dials : 5;
            var registerMax = 1m;
            for (var i = 0; i < dials; i++) registerMax *= 10m;

            var reading = previous.Reading + consumption / multiplier;
            if (reading >= registerMax)
            {
                reading -= registerMax;
            }

            return new MeterRead
            {
                MeterSerial = previous.MeterSerial,
                ReadDate = readDate.Date,
                Reading = Math.Round(reading, 1),
                ReadType = ReadType.Estimated,
                Source = ReadSource.Engine,
                ReaderCode = "EST"
            };
        }
    }
}
