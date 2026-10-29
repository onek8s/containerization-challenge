using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ContosoFoods.TradeRebates.Rules
{
    /// <summary>How a tier rate is applied once a threshold is reached.</summary>
    public enum TierMode : byte
    {
        /// <summary>The rate of the highest tier reached applies to the whole measure.</summary>
        Retroactive = (byte)'R',

        /// <summary>Each band is rebated at its own rate (marginal).</summary>
        Stepped = (byte)'S'
    }

    public sealed class Tier
    {
        public Tier(decimal threshold, decimal ratePercent)
        {
            Threshold = threshold;
            RatePercent = ratePercent;
        }

        /// <summary>Lower bound of the tier (units or currency, depending on the agreement basis).</summary>
        public decimal Threshold { get; }

        /// <summary>Rebate rate in percent of the net purchase value.</summary>
        public decimal RatePercent { get; }
    }

    /// <summary>
    /// Rebate tier table of an agreement. Text form: <c>R;0:0;10000:1.5;25000:2.25</c>
    /// (mode, then threshold:rate pairs in ascending threshold order).
    /// The binary form is the storage format of the rebate.TierSchedule column type and must not change.
    /// </summary>
    public sealed class TierSchedule
    {
        public const int MaxTiers = 20;
        private const byte FormatVersion = 1;

        private readonly Tier[] _tiers;

        public TierSchedule(TierMode mode, IEnumerable<Tier> tiers)
        {
            if (mode != TierMode.Retroactive && mode != TierMode.Stepped)
                throw new ArgumentOutOfRangeException(nameof(mode));

            var list = new List<Tier>(tiers ?? throw new ArgumentNullException(nameof(tiers)));
            if (list.Count == 0)
                throw new FormatException("A tier schedule needs at least one tier.");
            if (list.Count > MaxTiers)
                throw new FormatException("A tier schedule can have at most " + MaxTiers + " tiers.");
            if (list[0].Threshold != 0m)
                throw new FormatException("The first tier must start at 0.");

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].RatePercent < 0m || list[i].RatePercent > 100m)
                    throw new FormatException("Tier rate must be between 0 and 100 percent.");
                if (i > 0 && list[i].Threshold <= list[i - 1].Threshold)
                    throw new FormatException("Tier thresholds must be in ascending order.");
            }

            Mode = mode;
            _tiers = list.ToArray();
        }

        public TierMode Mode { get; }

        public IReadOnlyList<Tier> Tiers => _tiers;

        public static TierSchedule Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new FormatException("Tier schedule text is empty.");

            string[] parts = text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            string modeToken = parts[0].Trim().ToUpperInvariant();
            TierMode mode;
            if (modeToken == "R" || modeToken == "RETRO" || modeToken == "RETROACTIVE")
                mode = TierMode.Retroactive;
            else if (modeToken == "S" || modeToken == "STEP" || modeToken == "STEPPED")
                mode = TierMode.Stepped;
            else
                throw new FormatException("Unknown tier mode '" + parts[0].Trim() + "'. Use R or S.");

            var tiers = new List<Tier>();
            for (int i = 1; i < parts.Length; i++)
            {
                string[] pair = parts[i].Split(':');
                if (pair.Length != 2)
                    throw new FormatException("Tier '" + parts[i].Trim() + "' must be threshold:rate.");

                decimal threshold = ParseNumber(pair[0]);
                decimal rate = ParseNumber(pair[1].TrimEnd().TrimEnd('%'));
                tiers.Add(new Tier(threshold, rate));
            }

            return new TierSchedule(mode, tiers);
        }

        public static bool TryParse(string text, out TierSchedule? schedule, out string? error)
        {
            try
            {
                schedule = Parse(text);
                error = null;
                return true;
            }
            catch (FormatException ex)
            {
                schedule = null;
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Rate of the tier reached by <paramref name="measure"/>.</summary>
        public decimal RateFor(decimal measure)
        {
            decimal rate = _tiers[0].RatePercent;
            foreach (Tier tier in _tiers)
            {
                if (measure >= tier.Threshold)
                    rate = tier.RatePercent;
                else
                    break;
            }
            return rate;
        }

        /// <summary>
        /// Rebate amount for a period. <paramref name="measure"/> selects the tiers (volume or value),
        /// <paramref name="value"/> is the net purchase value the rate is applied to.
        /// </summary>
        public decimal RebateFor(decimal measure, decimal value)
        {
            if (measure <= 0m || value <= 0m)
                return 0m;

            if (Mode == TierMode.Retroactive)
                return Math.Round(value * RateFor(measure) / 100m, 2, MidpointRounding.AwayFromZero);

            // Stepped: weight each band's rate by the share of the measure falling into it.
            decimal weighted = 0m;
            for (int i = 0; i < _tiers.Length; i++)
            {
                decimal lower = _tiers[i].Threshold;
                if (measure <= lower)
                    break;
                decimal upper = i + 1 < _tiers.Length ? _tiers[i + 1].Threshold : decimal.MaxValue;
                decimal inBand = Math.Min(measure, upper) - lower;
                weighted += inBand * _tiers[i].RatePercent;
            }

            decimal effectiveRate = weighted / measure;
            return Math.Round(value * effectiveRate / 100m, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>Effective rate actually earned for the period, in percent.</summary>
        public decimal EffectiveRate(decimal measure, decimal value)
        {
            if (value <= 0m)
                return 0m;
            return Math.Round(RebateFor(measure, value) * 100m / value, 4, MidpointRounding.AwayFromZero);
        }

        /// <summary>Distance to the next tier threshold, or null when the top tier is reached.</summary>
        public decimal? ToNextTier(decimal measure)
        {
            foreach (Tier tier in _tiers)
            {
                if (tier.Threshold > measure)
                    return tier.Threshold - measure;
            }
            return null;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(FormatVersion);
            writer.Write((byte)Mode);
            writer.Write((byte)_tiers.Length);
            foreach (Tier tier in _tiers)
            {
                writer.Write(tier.Threshold);
                writer.Write(tier.RatePercent);
            }
        }

        public static TierSchedule Read(BinaryReader reader)
        {
            byte version = reader.ReadByte();
            if (version != FormatVersion)
                throw new InvalidDataException("Unsupported tier schedule format " + version + ".");

            var mode = (TierMode)reader.ReadByte();
            int count = reader.ReadByte();
            var tiers = new List<Tier>(count);
            for (int i = 0; i < count; i++)
            {
                decimal threshold = reader.ReadDecimal();
                decimal rate = reader.ReadDecimal();
                tiers.Add(new Tier(threshold, rate));
            }
            return new TierSchedule(mode, tiers);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append((char)(byte)Mode);
            foreach (Tier tier in _tiers)
            {
                sb.Append(';')
                  .Append(tier.Threshold.ToString("0.##", CultureInfo.InvariantCulture))
                  .Append(':')
                  .Append(tier.RatePercent.ToString("0.####", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static decimal ParseNumber(string text)
        {
            if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                throw new FormatException("'" + text.Trim() + "' is not a number.");
            return value;
        }
    }
}
