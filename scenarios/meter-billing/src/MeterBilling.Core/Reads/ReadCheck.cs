using System;
using System.Collections.Generic;

namespace Coho.Billing.Core.Reads
{
    public static class ReadExceptionCodes
    {
        public const string Negative = "NEGATIVE";
        public const string High = "HIGH";
        public const string Low = "LOW";
        public const string Zero = "ZERO";
        public const string OutOfSequence = "OUT_OF_SEQUENCE";
        public const string FutureDate = "FUTURE_DATE";
        public const string Leak = "LEAK";
        public const string Tamper = "TAMPER";
    }

    public class ReadCheckSettings
    {
        public decimal HighFactor { get; set; } = 3.0m;
        public decimal LowFactor { get; set; } = 0.2m;

        /// <summary>High and low checks are skipped below this consumption (m3), small users vary a lot.</summary>
        public decimal MinimumCheckedConsumption { get; set; } = 5m;

        /// <summary>A negative advance is treated as a rollover only when the previous reading was in the top part of the register.</summary>
        public decimal RolloverWindow { get; set; } = 0.1m;
    }

    public class ReadCheckResult
    {
        public ReadCheckResult()
        {
            Exceptions = new List<string>();
        }

        public decimal Consumption { get; set; }
        public int Days { get; set; }
        public bool Rollover { get; set; }
        public List<string> Exceptions { get; private set; }

        public bool Passed
        {
            get { return Exceptions.Count == 0; }
        }
    }

    public class ReadValidator
    {
        private readonly ReadCheckSettings _settings;

        public ReadValidator(ReadCheckSettings settings)
        {
            _settings = settings ?? new ReadCheckSettings();
        }

        public ReadCheckResult Check(BillingAccount account, MeterRead previous, MeterRead current, DateTime today)
        {
            if (account == null) throw new ArgumentNullException("account");
            if (previous == null) throw new ArgumentNullException("previous");
            if (current == null) throw new ArgumentNullException("current");

            var result = new ReadCheckResult { Days = (current.ReadDate.Date - previous.ReadDate.Date).Days };

            if (current.ReadDate.Date > today.Date)
            {
                result.Exceptions.Add(ReadExceptionCodes.FutureDate);
            }

            if (result.Days <= 0)
            {
                result.Exceptions.Add(ReadExceptionCodes.OutOfSequence);
                return result;
            }

            var dials = account.Meter != null && account.Meter.Dials > 0 ? account.Meter.Dials : 5;
            var multiplier = account.Meter != null && account.Meter.Multiplier > 0 ? account.Meter.Multiplier : 1m;
            var registerMax = Pow10(dials);

            var advance = current.Reading - previous.Reading;
            if (advance < 0)
            {
                if (previous.Reading >= registerMax * (1 - _settings.RolloverWindow) && current.Reading < registerMax * _settings.RolloverWindow)
                {
                    advance = current.Reading + registerMax - previous.Reading;
                    result.Rollover = true;
                }
                else
                {
                    result.Consumption = advance * multiplier;
                    result.Exceptions.Add(ReadExceptionCodes.Negative);
                    return result;
                }
            }

            result.Consumption = advance * multiplier;

            if (current.ReaderCode == "LK")
            {
                result.Exceptions.Add(ReadExceptionCodes.Leak);
            }
            else if (current.ReaderCode == "A01" || current.ReaderCode == "A02")
            {
                result.Exceptions.Add(ReadExceptionCodes.Tamper);
            }

            var expected = account.AverageDailyUse * result.Days;
            if (expected <= 0)
            {
                return result;
            }

            if (result.Consumption == 0)
            {
                result.Exceptions.Add(ReadExceptionCodes.Zero);
            }
            else if (Math.Max(result.Consumption, expected) >= _settings.MinimumCheckedConsumption)
            {
                if (result.Consumption > expected * _settings.HighFactor)
                {
                    result.Exceptions.Add(ReadExceptionCodes.High);
                }
                else if (result.Consumption < expected * _settings.LowFactor)
                {
                    result.Exceptions.Add(ReadExceptionCodes.Low);
                }
            }

            return result;
        }

        private static decimal Pow10(int n)
        {
            var value = 1m;
            for (var i = 0; i < n; i++)
            {
                value *= 10m;
            }

            return value;
        }
    }
}
